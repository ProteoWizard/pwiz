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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#include "stdafx.h"
#include "tagrecon.h"
#include "UniModXMLParser.h"
#include "BlosumMatrix.h"
#include "DeltaMasses.h"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/utility/proteome/Version.hpp"
#include "PTMVariantList.h"
#include "svnrev.hpp"


namespace freicore
{
    namespace tagrecon
    {
        WorkerThreadMap	            g_workerThreads;
        simplethread_mutex_t	    resourceMutex;

        proteinStore			    proteins;
        SpectraList			        spectra;
        SpectraTagMap		        spectraTagMapsByChargeState;

        RunTimeConfig*      g_rtConfig;

        tagIndex_t					tagIndex;
        tagMetaIndex_t				tagMetaIndex;
        tagMutexes_t				tagMutexes;
        string						tagIndexFilename;

        modMap_t					knownModifications;
        UniModXMLParser*			unimodXMLParser;
        DeltaMasses*				deltaMasses;
        BlosumMatrix*				blosumMatrix;

        int Version::Major()                {return 1;}
        int Version::Minor()                {return 2;}
        int Version::Revision()             {return SVN_REV;}
        string Version::LastModified()      {return SVN_REVDATE;}
        string Version::str()               
        {
            std::ostringstream v;
            v << Major() << "." << Minor() << "." << Revision();
            return v.str();
        }

        /**!
        WriteOutputToFile writes the results of TagRecon to an XML file. The XML file is formatted as
        PepXML. The function first computes the e-values of the peptide hits, computes the ranks of
        peptide hits for each spectrum, and converts the protein indices of each petpide hit to its
        corresponding protein annotation. The function also write out an SVG file for each top hit of 
        the spectrum. The function also write out auxillary files that are used to asses the performance
        of the deisotoping and precursor m/z adjustment. 
        */
        void WriteOutputToFile(	const string& dataFilename,
            string startTime,
            string startDate,
            float totalSearchTime,
            vector< size_t > opcs,
            vector< size_t > fpcs,
            searchStats& overallStats )
        {
            int numSpectra = 0;
            int numMatches = 0;
            int numLoci = 0;

            string filenameAsScanName = basename( MAKE_PATH_FOR_BOOST(dataFilename) );

            // Make histograms of scores by charge state
            map< int, Histogram<float> > meanScoreHistogramsByChargeState;
		    for( int z=1; z <= g_rtConfig->maxChargeStateFromSpectra; ++z )
                meanScoreHistogramsByChargeState[ z ] = Histogram<float>( g_rtConfig->NumScoreHistogramBins, g_rtConfig->MaxScoreHistogramValues );

            // Compute the e-value if required by the user 
            if( g_rtConfig->CalculateRelativeScores )
            {
                Timer calculationTime(true);
                cout << g_hostString << " is calculating relative scores for " << spectra.size() << " spectra." << endl;
                float lastUpdateTime = 0;
                size_t n = 0;
                // For each spectrum
                for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr, ++n )
                {
                    Spectrum* s = (*sItr);

                    try
                    {
                        // Compute the e-value
                        s->CalculateRelativeScores();
                    } catch( exception& e )
                    {
                        //throw runtime_error( "calculating relative scores for scan " + string( s->id ) + ": " + e.what() );
                        cerr << "Error: calculating relative scores for scan " << string( s->id ) << ": " << e.what() << endl;
                        continue;
                    } catch( ... )
                    {
                        cerr << "Error: calculating relative scores for scan " << string( s->id ) << endl;
                        continue;
                    }

                    if( calculationTime.TimeElapsed() - lastUpdateTime > g_rtConfig->StatusUpdateFrequency )
                    {
                        cout << g_hostString << " has calculated relative scores for " << n << " of " << spectra.size() << " spectra." << endl;
                        lastUpdateTime = calculationTime.TimeElapsed();
                    }
                    PRINT_PROFILERS(cout, s->id.id + " done");
                }
                cout << g_hostString << " finished calculating relative scores; " << calculationTime.End() << " seconds elapsed." << endl;
            }

            // For each spectrum
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
            {
                ++ numSpectra;
                Spectrum* s = (*sItr);

                // Set the spectrum id as the scan number
                spectra.setId( s->id, SpectrumId( filenameAsScanName, s->id.index, s->id.charge ) );

                // Compute the relative ranks of the results in the result set
                s->resultSet.calculateRanks();
                // Convert all the protein indices in the result set to their corresponding names
                s->resultSet.convertProteinIndexesToNames( proteins.indexToName );

                // Compute score histograms
                if( g_rtConfig->MakeScoreHistograms )
                {
                    s->scoreHistogram.smooth();
                    for( map<float,int>::iterator itr = s->scores.begin(); itr != s->scores.end(); ++itr )
                        cout << itr->first << "\t" << itr->second << "\n";
                    //cout << std::keys( s->scores ) << endl;
                    //cout << std::values( s->scores ) << endl;
                    //cout << std::keys( s->scoreHistogram.m_bins ) << endl;
                    //cout << std::values( s->scoreHistogram.m_bins ) << endl;
                    //s->scoreHistogram.writeToSvgFile( string( s->id ) + "-histogram.svg", "MVH score", "Density", 800, 600 );
                    meanScoreHistogramsByChargeState[ s->id.charge ] += s->scoreHistogram;
                }

                // Iterate through the result set from backwards (i.e highest scoring peptide)
                for( Spectrum::SearchResultSetType::reverse_iterator itr = s->resultSet.rbegin(); itr != s->resultSet.rend(); ++itr )
                {
                    ++ numMatches;
                    numLoci += itr->lociByName.size();

                    string theSequence = itr->sequence();

                    // Make a spectrum graph for the top scoring interpretation. The spectrum
                    // graph is created as an SVG formatted file.
                    if( itr->rank == 1 && g_rtConfig->MakeSpectrumGraphs )
                    {
                        vector< double > ionMasses;
                        vector< string > ionNames;
                        // Compute the predicted ions for the interpretation.
                        //const bool allIonTypes[4] = { true, true, false, false };
                        CalculateSequenceIons( Peptide(theSequence), s->id.charge, &ionMasses, s->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, &ionNames, 0 );
                        map< double, string > ionLabels;
                        map< double, string > ionColors;
                        map< double, int > ionWidths;

                        // Set the width of the ion strokes
                        for( PeakPreData::iterator itr = s->peakPreData.begin(); itr != s->peakPreData.end(); ++itr )
                            ionWidths[ itr->first ] = 1;
                        cout << ionMasses << endl << ionNames << endl;
                        // For each ion in the predicted spectrum find a peak in the experimental spectrum
                        // that is close to it.
                        for( size_t i=0; i < ionMasses.size(); ++i )
                        {
                            PeakPreData::iterator itr = s->peakPreData.findNear( ionMasses[i], g_rtConfig->FragmentMzTolerance );
                            // Assign the color depending on which ion type we matched.
                            if( itr != s->peakPreData.end() )
                            {
                                ionLabels[ itr->first ] = ionNames[i];
                                ionColors[ itr->first ] = ( ionNames[i].find( "b" ) == 0 ? "red" : "blue" );
                                ionWidths[ itr->first ] = 2;
                            }
                        }

                        cout << theSequence << " fragment ions: " << ionLabels << endl;

                        // Write the spectrum to a SVG formatted file
                        s->writeToSvgFile( string( "-" ) + theSequence + g_rtConfig->OutputSuffix, &ionLabels, &ionColors, &ionWidths );
                    }
                }
            }

            // Write the score histograms to the SVG file
            if( g_rtConfig->MakeScoreHistograms )
			for( int z=1; z <= g_rtConfig->maxChargeStateFromSpectra; ++z )
                    meanScoreHistogramsByChargeState[ z ].writeToSvgFile( filenameAsScanName + g_rtConfig->OutputSuffix + "_+" + lexical_cast<string>(z) + "_histogram.svg", "MVH score", "Density", g_rtConfig->ScoreHistogramWidth, g_rtConfig->ScoreHistogramHeight );

            // Get some stats and program parameters
            RunTimeVariableMap vars = g_rtConfig->getVariables();
            RunTimeVariableMap fileParams;
            for( RunTimeVariableMap::iterator itr = vars.begin(); itr != vars.end(); ++itr )
                fileParams[ string("Config: ") + itr->first ] = itr->second;
            fileParams["SearchEngine: Name"] = "TagRecon";
            fileParams["SearchEngine: Version"] = Version::str();
            fileParams["SearchTime: Started"] = startTime + " on " + startDate;
            fileParams["SearchTime: Stopped"] = GetTimeString() + " on " + GetDateString();
            fileParams["SearchTime: Duration"] = lexical_cast<string>( totalSearchTime ) + " seconds";
            fileParams["SearchStats: Nodes"] = lexical_cast<string>( g_numProcesses );
            fileParams["SearchStats: Overall"] = (string) overallStats;
            fileParams["PeakCounts: Mean: Original"] = lexical_cast<string>( opcs[5] );
            fileParams["PeakCounts: Mean: Filtered"] = lexical_cast<string>( fpcs[5] );
            fileParams["PeakCounts: Min/Max: Original"] = lexical_cast<string>( opcs[0] ) + " / " + lexical_cast<string>( opcs[1] );
            fileParams["PeakCounts: Min/Max: Filtered"] = lexical_cast<string>( fpcs[0] ) + " / " + lexical_cast<string>( fpcs[1] );
            fileParams["PeakCounts: 1stQuartile: Original"] = lexical_cast<string>( opcs[2] );
            fileParams["PeakCounts: 1stQuartile: Filtered"] = lexical_cast<string>( fpcs[2] );
            fileParams["PeakCounts: 2ndQuartile: Original"] = lexical_cast<string>( opcs[3] );
            fileParams["PeakCounts: 2ndQuartile: Filtered"] = lexical_cast<string>( fpcs[3] );
            fileParams["PeakCounts: 3rdQuartile: Original"] = lexical_cast<string>( opcs[4] );
            fileParams["PeakCounts: 3rdQuartile: Filtered"] = lexical_cast<string>( fpcs[4] );

            // Output pepXML format
            string outputFilename = filenameAsScanName + g_rtConfig->OutputSuffix + ".pepXML";
            cout << g_hostString << " is writing search results to file \"" << outputFilename << "\"." << endl;
            spectra.writePepXml( dataFilename, g_rtConfig->OutputSuffix, "TagRecon", g_dbPath + g_dbFilename, &proteins, fileParams );

            // Auxillary file to test the performance of deisotoping
            if( g_rtConfig->DeisotopingMode == 3 /*&& g_rtConfig->DeisotopingTestMode != 0*/ )
            {
                // Compute the FDR for all charge states and get spectra that passes the
                // score threshold at 0.05 level.
			    spectra.calculateFDRs( g_rtConfig->maxChargeStateFromSpectra, 1.0f, "rev_" );
                SpectraList passingSpectra;
                spectra.filterByFDR( 0.05f, &passingSpectra );
                //g_rtConfig->DeisotopingMode = g_rtConfig->DeisotopingTestMode;

                // For each of the filtered spectrum
                ofstream deisotopingDetails( (filenameAsScanName+g_rtConfig->OutputSuffix+"-deisotope-test.tsv").c_str() );
                deisotopingDetails << "Scan\tCharge\tSequence\tPredicted\tMatchesBefore\tMatchesAfter\n";
                for( SpectraList::iterator sItr = passingSpectra.begin(); sItr != passingSpectra.end(); ++sItr )
                {
                    // Deisotope the spectrum
                    Spectrum* s = (*sItr);
                    s->Deisotope( g_rtConfig->IsotopeMzTolerance );

                    s->resultSet.calculateRanks();
                    // Iterate through the results
                    for( Spectrum::SearchResultSetType::reverse_iterator itr = s->resultSet.rbegin(); itr != s->resultSet.rend(); ++itr )
                    {
                        string theSequence = itr->sequence();

                        // Get the top hit
                        if( itr->rank == 1 )
                        {
                            // Compute the number of predicted fragments, matched fragments before deisotoping
                            // and matched fragments after deisotoping.
                            vector< double > ionMasses;
                            //const bool allIonTypes[4] = { true, true, true, true };
                            //CalculateSequenceIons( Peptide(theSequence), s->id.charge, &ionMasses, s->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0 );
                            CalculateSequenceIons( Peptide(theSequence), s->id.charge, &ionMasses, s->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0 );
                            int fragmentsPredicted = accumulate( itr->key.begin(), itr->key.end(), 0 );
                            int fragmentsFound = fragmentsPredicted - itr->key.back();
                            int fragmentsFoundAfterDeisotoping = 0;
                            for( size_t i=0; i < ionMasses.size(); ++i )
                                if( s->peakPreData.findNear( ionMasses[i], g_rtConfig->FragmentMzTolerance ) != s->peakPreData.end() )
                                    ++ fragmentsFoundAfterDeisotoping;
                            deisotopingDetails << s->id.index << "\t" << s->id.charge << "\t" << theSequence << "\t" << fragmentsPredicted << "\t" << fragmentsFound << "\t" << fragmentsFoundAfterDeisotoping << "\n";
                        }
                    }
                }
                passingSpectra.clear(false);
            }

            // Auxillary file to test the performance of precursor mass adjustment
            if( g_rtConfig->AdjustPrecursorMass == 1 )
            {
                // Compute the FDR and select spectra that passes the score thresold at 
                // an FDR of 0.05
			    spectra.calculateFDRs( g_rtConfig->maxChargeStateFromSpectra, 1.0f, "rev_" );
                SpectraList passingSpectra;
                spectra.filterByFDR( 0.05f, &passingSpectra );

                // For each of the spectrum that passes the FDR test
                ofstream adjustmentDetails( (filenameAsScanName+g_rtConfig->OutputSuffix+"-adjustment-test.tsv").c_str() );
                adjustmentDetails << "Scan\tCharge\tUnadjustedSequenceMass\tAdjustedSequenceMass\tUnadjustedPrecursorMass\tAdjustedPrecursorMass\tUnadjustedError\tAdjustedError\tSequence\n";
                for( SpectraList::iterator sItr = passingSpectra.begin(); sItr != passingSpectra.end(); ++sItr )
                {
                    Spectrum* s = (*sItr);

                    // Compute the ranks of the results
                    s->resultSet.calculateRanks();
                    for( Spectrum::SearchResultSetType::reverse_iterator itr = s->resultSet.rbegin(); itr != s->resultSet.rend(); ++itr )
                    {
                        // Get the top hit
                        if( itr->rank == 1 )
                        {
                            // Print out the mass of the sequence before and after the precursor m/z adjustment
                            double setSeqMass = g_rtConfig->UseAvgMassOfSequences ? itr->molecularWeight() : itr->monoisotopicMass();
                            double monoSeqMass = itr->monoisotopicMass();
                            adjustmentDetails <<	s->id.index << "\t" << s->id.charge << "\t" <<
                                setSeqMass << "\t" << monoSeqMass << "\t" << s->mOfUnadjustedPrecursor << "\t" << s->mOfPrecursor << "\t" <<
                                fabs( setSeqMass - s->mOfUnadjustedPrecursor ) << "\t" <<
                                fabs( monoSeqMass - s->mOfPrecursor ) << "\t" << itr->sequence() << "\n";
                        }
                    }
                }
                passingSpectra.clear(false);
            }
        }

        /**
        getInterpretation function takes DigestedPeptide and converts it into a string.
        It uses AminoAcid+ModMass notation to represent the location of mods in the sequence. 
        An example output for a peptide with an oxidized methonine would look like PVSPLLLASGM+16AR.
        This string is used for display and sorting purposes.
        */
        string getInterpretation(const DigestedPeptide& peptide) {

            string returnString;
            // Get the peptide sequence and the mods
            string baseString = peptide.sequence();
            ModificationMap& mods = const_cast <ModificationMap&> (peptide.modifications());
            // For each amino acid
            for(size_t aa = 0; aa < baseString.length(); aa++) {
                // Append the amino acid to the sequence
                returnString += baseString[aa];
                std::ostringstream os; 
                // Get the mods at the location of the amino acid
                for(ModificationList::iterator modIter = mods[aa].begin(); modIter != mods[aa].end(); modIter++) {
                    // Add the mass of the mod (rounded) after the amino acid
                    os << ((int) ((*modIter).monoisotopicDeltaMass()+((*modIter).monoisotopicDeltaMass()>0?0.5:-0.5)));
                }
                // Append the mod to the amino acid
                returnString += os.str();
            }

            // Get the modifications on n-terminus and add them 
            // to the list.
            string nTerminus = "(";
            // For each n-terminal mod
            if(mods.begin()!= mods.end() && mods.begin()->first==ModificationMap::NTerminus() ) {
                std::ostringstream os;
                os << (int) round(mods.begin()->second.monoisotopicDeltaMass(), 0);
                nTerminus += os.str();
            }

            // Get the modifications on c-termimus
            string cTerminus = ")";
            if( mods.rbegin() != mods.rend() && mods.rbegin()->first == ModificationMap::CTerminus() ) {
                std::ostringstream os;
                os << (int) (mods.rbegin()->second.monoisotopicDeltaMass()+0.5);
                cTerminus += os.str();
            }

            // Return the formed interpretation
            return (nTerminus+returnString+cTerminus);
        }

        /**! 
        InitProcess (argsList) loacates the working directory if present, detects the number of cpus
        available for the process, loads the default or user given parameters and amino acid residue
        masses for the search. The default values are overridden by command line arguments. The
        function also transmits or receives the configs depending upon whether it is a root process
        or a child process.
        */
        int InitProcess( vector <std::string> & args ) {

            //cout << g_hostString << " is initializing." << endl;
            if( g_pid == 0 )
            {
                cout << "TagRecon " << Version::str() << " (" << Version::LastModified() << ")\n" <<
                    "FreiCore " << freicore::Version::str() << " (" << freicore::Version::LastModified() << ")\n" <<
                    "ProteoWizard MSData " << pwiz::msdata::Version::str() << " (" << pwiz::msdata::Version::LastModified() << ")\n" <<
                    "ProteoWizard Proteome " << pwiz::proteome::Version::str() << " (" << pwiz::proteome::Version::LastModified() << ")\n" <<
                    TAGRECON_LICENSE << endl;
            }

		    g_rtConfig = new RunTimeConfig;
            g_rtConfig->executableFilepath = args[0];

            g_rtSharedConfig = (BaseRunTimeConfig*) g_rtConfig;
            g_numWorkers = GetNumProcessors();

            // First set the working directory, if provided
            for( size_t i=1; i < args.size(); ++i )
            {
                if( args[i] == "-workdir" && i+1 <= args.size() )
                {
                    chdir( args[i+1].c_str() );
                    args.erase( args.begin() + i );
                } else if( args[i] == "-cpus" && i+1 <= args.size() )
                {
                    //Get the number of cpus
                    g_numWorkers = atoi( args[i+1].c_str() );
                    args.erase( args.begin() + i );
                } else
                    continue;

                args.erase( args.begin() + i );
                --i;
            }

            //Read the parameters and residue masses if this process is a master process.
            if( g_pid == 0 )
            {
                for( size_t i=1; i < args.size(); ++i )
                {
                    if( args[i] == "-cfg" && i+1 <= args.size() )
                    {
                        //Initialize the parameters from .cfg file.
                        if( g_rtConfig->initializeFromFile( args[i+1] ) )
                        {
                            cerr << g_hostString << " could not find runtime configuration at \"" << args[i+1] << "\"." << endl;
                            return 1;
                        }
                        args.erase( args.begin() + i );

                    } else
                        continue;

                    args.erase( args.begin() + i );
                    --i;
                }

                //Check to make sure the user has given a DB and a set of spectra.
			if( args.size() < 2 )
                {
				cerr << "Not enough arguments.\nUsage: " << args[0] << " [-ProteinDatabase <FASTA protein database filepath>] <input filemask 1> [input filemask 2] ..." << endl;
                    return 1;
                }
		
                //Check to see if the search parameters have been initialized
                if( !g_rtConfig->initialized() )
                {
                    if( g_rtConfig->initializeFromFile() )
                    {
                        cerr << g_hostString << " could not find the default configuration file (hard-coded defaults in use)." << endl;
                    }
                }

                

                //If running on a cluster as a parent process then transmit the 
                //search parameters and residue masses to the child processes.
			#ifdef USE_MPI
                if( g_numChildren > 0 )
                    TransmitConfigsToChildProcesses();
			#endif

            } else // child process
            {
			#ifdef USE_MPI
                ReceiveConfigsFromRootProcess();
			#endif
            }

            // Command line overrides happen after config file has been distributed but before PTM parsing
            RunTimeVariableMap vars = g_rtConfig->getVariables();
            // Run through each of the variable and check if a new value has been specified for it 
            // using a command line option
            for( RunTimeVariableMap::iterator itr = vars.begin(); itr != vars.end(); ++itr )
            {
                string varName;
                varName += "-" + itr->first;

                for( size_t i=1; i < args.size(); ++i )
                {
                    if( args[i].find( varName ) == 0 && i+1 <= args.size() )
                    {
                        //cout << varName << " " << itr->second << " " << args[i+1] << endl;
                        itr->second = args[i+1];
                        args.erase( args.begin() + i );
                        args.erase( args.begin() + i );
                        --i;
                    }
                }
            }
            // Set the variables
            g_rtConfig->setVariables( vars );

		#ifdef DEBUG
            for( size_t i = 0; i < args.size(); i++) {
                cout << "args[" << i << "]:" << args[i] << "\n";
            }	
		#endif	
		
            // Dump the paramters if the user opts for it
            if( g_pid == 0 )
            {
                for( size_t i=1; i < args.size(); ++i )
                {
                    if( args[i] == "-dump" )
                    {
                        g_rtConfig->dump();
                        args.erase( args.begin() + i );
                        --i;
                    }
                }

                // Skip unintelligible arguments on the command line
                for( size_t i=1; i < args.size(); ++i )
                {
                    if( args[i][0] == '-' )
                    {
                        cerr << "Warning: ignoring unrecognized parameter \"" << args[i] << "\"" << endl;
                        args.erase( args.begin() + i );
                        --i;
                    }
                }
            }

	

            // Parse out the unimod xml document for modifications
            unimodXMLParser = new UniModXMLParser(g_rtConfig->UnimodXML);
            unimodXMLParser->parseDocument();
            deltaMasses = new DeltaMasses(unimodXMLParser->getModifications());
            deltaMasses->buildDeltaMassLookupTables();
            //deltaMasses->printMassToAminoAcidMap();
            //deltaMasses->printInterpretationMap();
            //exit(1);
            return 0;
        }

	
        /**! 
        InitWorkerGlobals() sorts the spectra by their ID, create a map of tags and spectra
        containing the tags, pre-computes the ln(x!) table where x is number of m/z bins.
        The precomputed ln factorial table is used in MVH scoring.
        */
        int InitWorkerGlobals()
        {
            // Sort the spectra by their ID
            spectra.sort( spectraSortByID() );

            if( spectra.empty() )
                return 0;

        for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
            g_rtConfig->maxChargeStateFromSpectra = max((*sItr)->id.charge, g_rtConfig->maxChargeStateFromSpectra);

        //Set the mass tolerances according to the charge state.
        g_rtConfig->PrecursorMassTolerance.clear();
        g_rtConfig->NTerminalMassTolerance.clear();
        g_rtConfig->CTerminalMassTolerance.clear();
        for( int z=1; z <= g_rtConfig->maxChargeStateFromSpectra; ++z )
        {
            g_rtConfig->PrecursorMassTolerance.push_back( g_rtConfig->PrecursorMzTolerance * z );
            g_rtConfig->NTerminalMassTolerance.push_back( g_rtConfig->NTerminusMzTolerance * z );
            g_rtConfig->CTerminalMassTolerance.push_back( g_rtConfig->CTerminusMzTolerance * z );
        }

            // Get the number of spectra
            //size_t numSpectra = spectra.size();
            spectraTagMapsByChargeState = SpectraTagMap(TagSetCompare(max(g_rtConfig->MaxModificationMassPlus,g_rtConfig->MaxModificationMassMinus)));
            /* Create a map of precursor masses to the spectrum indices and map of tags and spectra with the
            tag. while doing that we also create a map of precursor masses and spectra with the precursor 
            mass. Precursor mass map is used to rapidly find candidates peptide sequences that are with in
            a wide mass tolerance (+/- MaxTagMassDeviation Da). After	the initial filtering step, tag map 
            is used to further filter out spectra that doesn't have any matching tags with the candidate 
            peptide sequences. 
            */
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr ) {
                // Get the tags for the spectrum and put them in the tag map.
                for( TagList::iterator tItr = (*sItr)->tagList.begin(); tItr != (*sItr)->tagList.end(); ++tItr ) {
                    TagSetInfo  tagInfo(sItr, tItr->tag, tItr->nTerminusMass, tItr->cTerminusMass);
                    tagInfo.tagChargeState = tItr->chargeState;
                    //spectraTagMapsByChargeState.insert( SpectraTagMap::value_type(TagSetInfo( sItr, tItr->tag, tItr->nTerminusMass, tItr->cTerminusMass ) ) );
                    spectraTagMapsByChargeState.insert( SpectraTagMap::value_type( tagInfo ) );
                }
            }
		
            /*if(false) {
            for(SpectraTagMap::const_iterator itr = spectraTagMapsByChargeState.begin(); itr != spectraTagMapsByChargeState.end(); ++itr) {
				cout << itr->candidateTag << "," << itr->nTerminusMass << "," << itr->cTerminusMass << (*itr->sItr)->id.source << " " << (*itr->sItr)->id.index << endl;
            }
            }*/

            // Get minimum and maximum peptide masses observed in the dataset
            // and determine the number of peak bins required. This 
            g_rtConfig->curMinSequenceMass = spectra.front()->mOfPrecursor;
            g_rtConfig->curMaxSequenceMass = 0;

            size_t maxPeakBins = (size_t) spectra.front()->totalPeakSpace;
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
            {
                if( (*sItr)->mOfPrecursor < g_rtConfig->curMinSequenceMass )
                    g_rtConfig->curMinSequenceMass = (*sItr)->mOfPrecursor;

                if( (*sItr)->mOfPrecursor > g_rtConfig->curMaxSequenceMass )
                    g_rtConfig->curMaxSequenceMass = (*sItr)->mOfPrecursor;

                size_t totalPeakBins = (size_t) round( (*sItr)->totalPeakSpace / ( g_rtConfig->FragmentMzTolerance * 2.0f ) );
                if( totalPeakBins > maxPeakBins )
                    maxPeakBins = totalPeakBins;
            }

            g_rtConfig->curMinSequenceMass -= g_rtConfig->PrecursorMassTolerance.back();
            g_rtConfig->curMaxSequenceMass += g_rtConfig->PrecursorMassTolerance.back();

            // set the effective minimum and maximum sequence masses based on config and precursors
            g_rtConfig->curMinSequenceMass = max( g_rtConfig->curMinSequenceMass, g_rtConfig->MinSequenceMass );
            g_rtConfig->curMaxSequenceMass = min( g_rtConfig->curMaxSequenceMass, g_rtConfig->MaxSequenceMass );

            double minResidueMass = AminoAcid::Info::record('G').residueFormula.monoisotopicMass();
            double maxResidueMass = AminoAcid::Info::record('W').residueFormula.monoisotopicMass();

            // calculate minimum length of a peptide made entirely of tryptophan over the minimum mass
            int curMinCandidateLength = max( g_rtConfig->MinCandidateLength,
                (int) floor( g_rtConfig->curMinSequenceMass /
                maxResidueMass ) );

            // calculate maximum length of a peptide made entirely of glycine under the maximum mass
            int curMaxCandidateLength = min((int) ceil( g_rtConfig->curMaxSequenceMass / minResidueMass ), 
                g_rtConfig->MaxSequenceLength);

            // set digestion parameters
            Digestion::Specificity specificity = (Digestion::Specificity) g_rtConfig->NumMinTerminiCleavages;
            g_rtConfig->NumMaxMissedCleavages = g_rtConfig->NumMaxMissedCleavages < 0 ? 10000 : g_rtConfig->NumMaxMissedCleavages;
            g_rtConfig->digestionConfig = Digestion::Config( g_rtConfig->NumMaxMissedCleavages,
                curMinCandidateLength,
                curMaxCandidateLength,
                specificity );

            //cout << g_hostString << " is precaching factorials up to " << (int) maxPeakSpace << "." << endl;
		
            // Calculate the ln(x!) table where x= number of m/z spaces.
            // This table is used in MVH scoring.
            g_lnFactorialTable.resize( maxPeakBins );
            //cout << g_hostString << " finished precaching factorials." << endl;

            if( !g_numChildren )
            {
                cout << "Smallest observed precursor is " << g_rtConfig->curMinSequenceMass << " Da." << endl;
                cout << "Largest observed precursor is " << g_rtConfig->curMaxSequenceMass << " Da." << endl;
                cout << "Min. effective sequence mass is " << g_rtConfig->curMinSequenceMass << endl;
                cout << "Max. effective sequence mass is " << g_rtConfig->curMaxSequenceMass << endl;
                cout << "Min. effective sequence length is " << curMinCandidateLength << endl;
                cout << "Max. effective sequence length is " << curMaxCandidateLength << endl;
            }

            //cout << "tagMapSize:" << spectraTagMapsByChargeState.size() << endl;

            return 0;
        }

        void DestroyWorkerGlobals()
        {
        }

	
        /**!
        PrepareSpectra parses out all the spectra in an input file, deterimes the
        charge states (user configurable), preprocesses the spectra, and trims out
        the spectra with too few peaks.
        */
        void PrepareSpectra()
        {
            // Get total spectra size
            int numSpectra = (int) spectra.size();

            Timer timer;
            //Running in a single processor mode
            if( g_numChildren == 0 )
                cout << g_hostString << " is parsing " << numSpectra << " spectra." << endl;

            timer.Begin();
            // Parse each spectrum
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
            {
                try
                {
                    (*sItr)->parse();
                } catch( exception& e )
                {
                    stringstream msg;
                    msg << "parsing spectrum " << (*sItr)->id << ": " << e.what();
                    throw runtime_error( msg.str() );
                } catch( ... )
                {
                    stringstream msg;
                    msg << "parsing spectrum " << (*sItr)->id;
                    throw runtime_error( msg.str() );
                }
            }

            if( g_numChildren == 0 )
            {
                cout << g_hostString << " finished parsing its spectra; " << timer.End() << " seconds elapsed." << endl;
                cout << g_hostString << " is trimming spectra with less than " << 10 << " peaks." << endl;
            }

            // Take out spectra that have less then 10 peaks
            int preTrimCount = spectra.filterByPeakCount( 10 );
            numSpectra = (int) spectra.size();

            if( g_numChildren == 0 )
            {
                cout << g_hostString << " trimmed " << preTrimCount << " spectra for being too sparse." << endl;
                cout << g_hostString << " is determining charge states for " << numSpectra << " spectra." << endl;
            }

            timer.Begin();
            SpectraList duplicates;
            // Try to determine the charge state for each spectrum
            // If you can't determine the charge state (i.e if z
            // state is not +1) then duplicate the spectrum to create
            // multiple charge states.
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
            {
                try
                {
                    if( !g_rtConfig->UseChargeStateFromMS )
                        spectra.setId( (*sItr)->id, SpectrumId( (*sItr)->id.source,(*sItr)->id.index, 0 ) );

                    if( (*sItr)->id.charge == 0 )
                    {
                        SpectrumId preChargeId( (*sItr)->id );
                        // Determine the charge state
                        (*sItr)->DetermineSpectrumChargeState();
                        SpectrumId postChargeId( (*sItr)->id );

                        // If the charge state is not +1
                        if( postChargeId.charge == 0 )
                        {
                            // Duplicate the spectrum and create
                            // spectrum with multiple charge states
                            postChargeId.setCharge(2);

                            if( g_rtConfig->DuplicateSpectra )
                            {
                                for( int z = 3; z <= g_rtConfig->NumChargeStates; ++z )
                                {
                                    Spectrum* s = new Spectrum( *(*sItr) );
                                    s->id.setCharge(z);
                                    duplicates.push_back(s);
                                }
                            }
                        }

                        spectra.setId( preChargeId, postChargeId );
                    }

                } catch( exception& e )
                {
                    throw runtime_error( string( "duplicating scan " ) + string( (*sItr)->id ) + ": " + e.what() );
                } catch( ... )
                {
                    throw runtime_error( string( "duplicating scan " ) + string( (*sItr)->id ) );
                }
            }

            try
            {
                // Add the created duplicates to the existing spectra list
                spectra.insert( duplicates.begin(), duplicates.end(), spectra.end() );
                duplicates.clear(false);
            } catch( exception& e )
            {
                throw runtime_error( string( "adding duplicated spectra: " ) + e.what() );
            } catch( ... )
            {
                throw runtime_error( "adding duplicated spectra" );
            }

            //int replicateCount = (int) spectra.size() - numSpectra;
            numSpectra = (int) spectra.size();

            if( g_numChildren == 0 )
            {
                cout << g_hostString << " finished determining charge states for its spectra; " << timer.End() << " seconds elapsed." << endl;
                cout << g_hostString << " is preprocessing " << numSpectra << " spectra." << endl;
            }

            timer.Begin();
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
            {
                try
                {
                    // Preprocess the spectrum (see the function documentation for further details)
                    (*sItr)->Preprocess();
                } catch( exception& e )
                {
                    stringstream msg;
                    msg << "preprocessing spectrum " << (*sItr)->id << ": " << e.what();
                    throw runtime_error( msg.str() );
                } catch( ... )
                {
                    stringstream msg;
                    msg << "preprocessing spectrum " << (*sItr)->id;
                    throw runtime_error( msg.str() );
                }
            }

            // Trim spectra that have observed precursor masses outside the user-configured range
            // (erase the peak list and the trim 0 peaks out)
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
            {
                if( (*sItr)->mOfPrecursor < g_rtConfig->MinSequenceMass ||
                    (*sItr)->mOfPrecursor > g_rtConfig->MaxSequenceMass )
                {
                    (*sItr)->peakPreData.clear();
                    (*sItr)->peakData.clear();
                }
            }

            if( g_numChildren == 0 )
            {
                // Throw some bones to the user to keep him occupied or disinterested.....
                cout << g_hostString << " finished preprocessing its spectra; " << timer.End() << " seconds elapsed." << endl;
                cout << g_hostString << " is trimming spectra with less than " << g_rtConfig->minIntensityClassCount << " peaks." << endl;
                cout << g_hostString << " is trimming spectra with precursors too small or large: " <<
                    g_rtConfig->MinSequenceMass << " - " << g_rtConfig->MaxSequenceMass << endl;
            }

            // Filter the spectra by peak count. If a spectrum doesn't have enough peaks to fill
            // out minimum number of intensity classes (user configurable) then the spectrum is
            // most likely a noisy spectrum. So, clip it off.
            int postTrimCount = spectra.filterByPeakCount( g_rtConfig->minIntensityClassCount );

            if( g_numChildren == 0 )
            {
                cout << g_hostString << " trimmed " << postTrimCount << " spectra." << endl;
            }
        }


        int numSearched;
        vector< int > workerNumbers;

        /**!
        GetTagsFromSequence takes a peptide sequence, its mass, and generates tags of specified length
        (user configurable) from the sequence. 
        */
        void GetTagsFromSequence( DigestedPeptide peptide, int tagLength, float seqMass, vector< TagInfo >& tags )
        {
            // Get the modification map and the sequence without mods.
            ModificationMap& mods = peptide.modifications();
            string seq = PEPTIDE_N_TERMINUS_STRING + peptide.sequence() + PEPTIDE_C_TERMINUS_STRING;
            vector<float> residueMasses(seq.length());

            residueMasses[0] = g_rtConfig->UseAvgMassOfSequences ? mods[mods.NTerminus()].averageDeltaMass() : mods[mods.NTerminus()].monoisotopicDeltaMass(); 
            for(size_t i = 1; i < residueMasses.size()-1; i++) {
                float modMass = g_rtConfig->UseAvgMassOfSequences ? mods[i-1].averageDeltaMass(): mods[i-1].monoisotopicDeltaMass();
                residueMasses[i] = (g_rtConfig->UseAvgMassOfSequences ? (AminoAcid::Info::record(seq[i])).residueFormula.molecularWeight():(AminoAcid::Info::record(seq[i])).residueFormula.monoisotopicMass()) + modMass;
            }

            //for(size_t i = 0; i < residueMasses.size(); ++i) {
            //    cout << residueMasses[i] << "," ;
            //}
            //cout << endl;
            size_t seqLength = seq.length();
            size_t maxTagStartIndex = seqLength - tagLength;

            float waterMass = WATER(g_rtConfig->UseAvgMassOfSequences);

            float runningNTerminalMass = 0.0f;
            for(size_t i = 0; i <= maxTagStartIndex; ++i) {
                float tagMass = 0.0f;
                for(size_t j = i; j < i+tagLength; j++) {
                    tagMass += residueMasses[j];
                }
                float cTerminalMass = seqMass - runningNTerminalMass - tagMass - waterMass;
                tags.push_back( TagInfo( seq.substr( i, tagLength ), runningNTerminalMass, cTerminalMass ) );
                tags.back().lowPeakMz = (float) i;
                //cout << seq.substr( i, tagLength ) << "," << runningNTerminalMass << "," << cTerminalMass << "," << tagMass << endl;
                runningNTerminalMass += residueMasses[i]; 
                //cout << runningNTerminalMass << endl;
            }
        }

        /**
        ScoreSubstitutionVariants takes a candidate peptide and delta mass. The procedure finds
        all the substitutions that can fit the delta mass, generates the variants, scores each
        of the generated variant against an experimental spectrum and stores the results.
        */
        inline boost::int64_t ScoreSubstitutionVariants(DigestedPeptide candidate, float mass, float modMass, 
            size_t locStart, size_t locEnd, Spectrum* spectrum, 
            int idx, vector<double>& sequenceIons, 
            const bool * ionTypesToSearchFor, float massTol) {

		
                boost::int64_t numComparisonsDone = 0;
                //cout << "\t\t\t\t\t" << candidate.sequence() << "," << modMass << "," << locStart << "," << locEnd << endl;
                // Get all possible amino acid substitutions that fit the modification mass with in the mass tolerance
                DynamicModSet possibleSubstitutions = deltaMasses->getPossibleSubstitutions(modMass, massTol);
                //cout << "\t\t\t\t\t" << possibleSubstitutions << endl;
                if(possibleSubstitutions.size() > 0) {
                    // Generate variants of the current peptide using the possible substitutions
                    vector <DigestedPeptide> substitutionVariants;
                    MakePeptideVariants(candidate, substitutionVariants, 1, possibleSubstitutions, locStart, locEnd, g_rtConfig->MaxNumPeptideVariants);
                    // For each variant
                    for(size_t aVariantIndex = 0; aVariantIndex < substitutionVariants.size(); aVariantIndex++) {
                        const DigestedPeptide& variant = substitutionVariants[aVariantIndex];
                        // Check to make sure that the insertion of sub doesn't put the mass of the peptide
                        // over the precursor mass tolerance.
                        float neutralMass = g_rtConfig->UseAvgMassOfSequences ? ((float) variant.molecularWeight(0,true))
                            : (float) variant.monoisotopicMass(0,true);
                        float massDiff = fabs(neutralMass - spectrum->mOfPrecursor);
                        if(massDiff > g_rtConfig->PrecursorMassTolerance[spectrum->id.charge]) {
                            continue;
                        }
                        string variantSequence = PEPTIDE_N_TERMINUS_SYMBOL + variant.sequence() + PEPTIDE_C_TERMINUS_SYMBOL;
                        //cout << "\t\t\t\t\t" << tagrecon::getInterpretation(const_cast <DigestedPeptide&>(variant)) << endl;
                        // Initialize the result
                        SearchResult result(variant);
                        // Compute the predicted spectrum and score it against the experimental spectrum
                        //CalculateSequenceIons( Peptide(theSequence), s->id.charge, &ionMasses, s->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, &ionNames, 0 );
                        CalculateSequenceIons( variant, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
                        //CalculateSequenceIons( variant, variantSequence, spectrum->id.charge, &sequenceIons, g_rtConfig->UseSmartPlusThreeModel, 0, 0, ionTypesToSearchFor );
                        spectrum->ScoreSequenceVsSpectrum( result, variantSequence, sequenceIons );
                        // Create the result
                        // Compute the true modification mass. The modMass of in the arguments is used to look up
                        // the canidate mods with a certain tolerance. It's not the true modification mass of the
                        // peptide.
                        float trueModificationMass = g_rtConfig->UseAvgMassOfSequences? variant.modifications().averageDeltaMass()-candidate.modifications().averageDeltaMass() : variant.modifications().monoisotopicDeltaMass() - candidate.modifications().monoisotopicDeltaMass();
                        result.mod = trueModificationMass;
                        result.massError = spectrum->mOfPrecursor-(mass+trueModificationMass);
                        // Assign the peptide identification to the protein by loci
                        result.lociByIndex.insert( ProteinLocusByIndex( idx + g_rtConfig->ProteinIndexOffset, candidate.offset() ) );
                        // cout << "\t\t\t\t\t" << result.mvh << "," << result.mzFidelity << endl;

                        ++ numComparisonsDone;

                        // Update some search stats and add the result to the
                        // spectrum
                        simplethread_lock_mutex( &spectrum->mutex );
                        if( proteins[idx].isDecoy() )
                            ++ spectrum->numDecoyComparisons;
                        else
                            ++ spectrum->numTargetComparisons;
                        spectrum->resultSet.add( result );
                        simplethread_unlock_mutex( &spectrum->mutex );
                    }
                }
                return numComparisonsDone;
        }


        /**
        ScoreUnknownModification takes a peptide sequence and localizes an unknown modification mass
        to a particular residue in the sequence. The number of tested resiudes is defined by locStart
        and locEnd variables of the procedure.
        */
	
        inline boost::int64_t ScoreUnknownModification(DigestedPeptide candidate, float mass, float modMass, 
            size_t locStart, size_t locEnd, Spectrum* spectrum, 
            int idx, vector<double>& sequenceIons, 
            const bool * ionTypesToSearchFor) {

		
                boost::int64_t numComparisonsDone = 0;
                DynamicModSet possibleInterpretations;
                string peptideSeq = PEPTIDE_N_TERMINUS_STRING + candidate.sequence() + PEPTIDE_C_TERMINUS_STRING;
		
                //cout << candidate.sequence() << "," << modMass << "," << locStart << "," << locEnd << endl;
                // For each amino acid between the location bounds
                for(size_t aaIndex = locStart; aaIndex <= locEnd; aaIndex++) {
                    // Add the modification to the amino acid
                    DynamicMod mod(peptideSeq[aaIndex],peptideSeq[aaIndex],modMass);
                    possibleInterpretations.insert(mod);
                }

                // Generate peptide variants of the current petpide using possible modifications
                vector<DigestedPeptide> modificationVariants;
                //cout << "Making variants" << endl;
                MakePeptideVariants(candidate, modificationVariants, 1, possibleInterpretations, locStart, locEnd, g_rtConfig->MaxNumPeptideVariants);
                // For each variant
                for(size_t variantIndex = 0; variantIndex < modificationVariants.size(); variantIndex++) {
                    const DigestedPeptide& variant = modificationVariants[variantIndex];
                    //cout << "\t" << variant.sequence() << endl;
                    string variantSequence = PEPTIDE_N_TERMINUS_SYMBOL + variant.sequence() + PEPTIDE_C_TERMINUS_SYMBOL;
                    // Initialize search result
                    SearchResult result(variant);
                    // Compute the predicted spectrum and score it against the experimental spectrum
                    //CalculateSequenceIons( variant, variantSequence, spectrum->id.charge, &sequenceIons, g_rtConfig->UseSmartPlusThreeModel, 0, 0, ionTypesToSearchFor );
                    CalculateSequenceIons( variant, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
                    spectrum->ScoreSequenceVsSpectrum( result, variantSequence, sequenceIons );
                    // Assign the modification mass and the mass error
                    // Compute the true modification mass. The modMass of in the arguments is used to look up
                    // the canidate mods with a certain tolerance. It's not the true modification mass of the
                    // peptide.
                    float trueModificationMass = g_rtConfig->UseAvgMassOfSequences? variant.modifications().averageDeltaMass()-candidate.modifications().averageDeltaMass() : variant.modifications().monoisotopicDeltaMass() - candidate.modifications().monoisotopicDeltaMass();
                    result.mod = trueModificationMass;
                    result.massError = spectrum->mOfPrecursor-(mass+trueModificationMass);
                    // Assign the peptide identification to the protein by loci
                    result.lociByIndex.insert( ProteinLocusByIndex( idx + g_rtConfig->ProteinIndexOffset, candidate.offset() ) );

                    ++ numComparisonsDone;

                    // Update some search stats and add the result to the
                    // spectrum
                    simplethread_lock_mutex( &spectrum->mutex );
                    if( proteins[idx].isDecoy() )
                        ++ spectrum->numDecoyComparisons;
                    else
                        ++ spectrum->numTargetComparisons;
                    spectrum->resultSet.add( result );
                    simplethread_unlock_mutex( &spectrum->mutex );
                }

                // Return the number of comparisons performed
                return numComparisonsDone;
        }

        /**!
        QuerySequence function takes a candidate peptide sequence as input. It generates all n length tags from 
        the peptide sequence. For each of the generated tags, it locates the spectra that contain the tag. It 
        computes the n-terminal and c-terminal delta masses between the candidate peptide sequence and spectral
        tag-based sequence sourrounding the tag match. 
        For example:
        //    XXXXXXXXNSTXXXXXXXX (peptide sequence)
        //            |||		  (Tag match)
        //	  [200.45]NST[400.65] (spectral tag-based sequence)
        If either n-terminal or c-terminal delta mass is greater than set mass tolerance then the function 
        interprets either of them using a variable modification list supplied by the user. The function can 
        also explain the delta mass without the help of a user-supplied	variable modification list. If there
        are no non-zero delta masses then the function interprets the peptide match as an unmodified peptide.
        */
        boost::int64_t QuerySequence( const DigestedPeptide& candidate, int idx, bool estimateComparisonsOnly = false )
        {
            // Search stats
            boost::int64_t numComparisonsDone = 0;
            // Candidate peptide sequence and its mass
            string aSequence  = PEPTIDE_N_TERMINUS_STRING + candidate.sequence() + PEPTIDE_C_TERMINUS_STRING;
            string seq = PEPTIDE_N_TERMINUS_STRING + candidate.sequence() + PEPTIDE_C_TERMINUS_STRING;
            //cout << aSequence << "," << aSequence.length() << "," << candidate.sequence() << "," << candidate.sequence().length() << endl;
            float neutralMass = g_rtConfig->UseAvgMassOfSequences ? ((float) candidate.molecularWeight(0,true))
                : (float) candidate.monoisotopicMass(0,true);

            // A vector to store fragment ions by charge state
            vector< double > fragmentIonsByChargeState;
        	vector< double >& sequenceIons = fragmentIonsByChargeState;

			// A spectrum pointer

            Spectrum* spectrum;
            // A data structure to store the results
            SearchResult result(candidate);
            // A variable to hold the number of common peaks between hypothetical
            // and experimental spectrum
		//size_t peaksFound;
            // Ion types to search for {y, b, [y-H2O,b-H2O], [y-NH3,b-NH3]}
            static const bool ionTypesToSearchFor[4] = { true, true, false, false };

        if( g_rtConfig->MassReconMode )
        {
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr)
            {
                spectrum = *sItr;
                float modMass = ((float)spectrum->mOfPrecursor) - neutralMass;
               
			    // Don't bother interpreting the mod mass if it's outside the user-set
                // limits.
                if( modMass < -1.0*g_rtConfig->MaxModificationMassMinus || modMass > g_rtConfig->MaxModificationMassPlus ) {
                    continue;
                }

                if(g_rtConfig->FindUnknownMods || g_rtConfig->FindSequenceVariations)
                {
                    // Figure out if the modification mass does nullify
                    // any dynamic mods. For example, M+16ILFEGHFK peptide
                    // can not have a modification of -16 on M. 
                    bool legitimateModMass = true;
                    // Get the dynamic mods
                    const ModificationMap& dynamicMods = candidate.modifications();
                    // Step through each of the dynamic mods
                    for( ModificationMap::const_iterator itr = dynamicMods.begin(); itr != dynamicMods.end() && legitimateModMass ; ++itr )
                    {
                        // Compute the mod mass
                        float residueModMass = g_rtConfig->UseAvgMassOfSequences ? itr->second.averageDeltaMass() : itr->second.monoisotopicDeltaMass();
                        // Check to make sure that the unknown mod mass doesn't nullify the dynamic mod mass
                        if(fabs(residueModMass+modMass) <= g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1])
                            legitimateModMass = false;
                    }

				    // Make sure that the modification mass doesn't negate the mods already present in the peptide
				    float candidateModificationsMass = g_rtConfig->UseAvgMassOfSequences ? candidate.modifications().averageDeltaMass() : candidate.modifications().monoisotopicDeltaMass();
				    if(fabs(candidateModificationsMass-modMass)< max((float)g_rtConfig->MinModificationMass, g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1]))
					    legitimateModMass = false;

                    if( legitimateModMass &&
                        fabs(modMass) > max((float)g_rtConfig->MinModificationMass, g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1]) )
                    {
                        // If the user configured the searches for substitutions
                        if(g_rtConfig->FindSequenceVariations)
                        {
                            numComparisonsDone +=
                                ScoreSubstitutionVariants(candidate, neutralMass, modMass,
                                                          0, seq.length(), spectrum,
                                                          idx, sequenceIons, ionTypesToSearchFor, g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1]);
                        }

                        // If the user wants us to find unknown modifications.
                        if(g_rtConfig->FindUnknownMods)
                        {
                            numComparisonsDone +=
                                ScoreUnknownModification(candidate, neutralMass, modMass,
                                                         0, seq.length(), spectrum, 
                                                         idx, sequenceIons, ionTypesToSearchFor);
                        }
                    }
                }

                if( fabs(modMass) <= g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1] )
                {
				    // If there are no n-terminal and c-terminal delta mass differences then
				    // score the match as an unmodified sequence.

                    CalculateSequenceIons( candidate, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
				    spectrum->ScoreSequenceVsSpectrum( result, aSequence, sequenceIons );

				    result.massError = spectrum->mOfPrecursor-neutralMass;
				    result.lociByIndex.insert( ProteinLocusByIndex( idx + g_rtConfig->ProteinIndexOffset, candidate.offset() ) );
				    ++numComparisonsDone;

				    if( g_rtConfig->UseMultipleProcessors )
					    simplethread_lock_mutex( &spectrum->mutex );
				    if( proteins[idx].isDecoy() )
					    ++ spectrum->numDecoyComparisons;
				    else
					    ++ spectrum->numTargetComparisons;
				    spectrum->resultSet.add( result );

				    if( g_rtConfig->UseMultipleProcessors )
					    simplethread_unlock_mutex( &spectrum->mutex );
			    }
            }

            // avoid tag-based querying
            //cout << numComparisonsDone << endl;
            return numComparisonsDone;
        }

            //set<SpectrumId> matchedSpectra;
            // Get tags of length 3 from the candidate peptide sequence
            vector< TagInfo > candidateTags;
            //GetTagsFromSequenceOld( seq, 3, neutralMass, candidateTags );
            GetTagsFromSequence( candidate, 3, neutralMass, candidateTags );
            //cout << "Query:TagMapSize:" <<spectraTagMapsByChargeState.size() << endl;
            //string comparisonDone;

            //cout << "peptide:" << seq << "->" << neutralMass << "->" << getInterpretation(candidate) << endl;
            // For each of the generated tags
            for( size_t i=0; i < candidateTags.size(); ++i )
            {
                const TagInfo& tag = candidateTags[i];

                // Get the range of spectral tags that have the same sequence as the peptide tag
                // and total mass deviation between the n-terminal and c-terminal masses <= +/-
                // MaxTagMassDeviation.
                TagSetInfo tagKey(tag.tag, tag.nTerminusMass, tag.cTerminusMass);
                //cout << "\t" << tagKey.candidateTag << "," << tagKey.nTerminusMass << "," << tagKey.cTerminusMass << endl;
                pair< SpectraTagMap::const_iterator, SpectraTagMap::const_iterator > range = spectraTagMapsByChargeState.equal_range( tagKey);

                SpectraTagMap::const_iterator cur, end = range.second;
			
                // Iterate over the range
                for( cur = range.first; cur != end; ++cur )
                {
				//cout << "\t\t" << (*cur->sItr)->id.source << " " << (*cur->sItr)->id.index << " " << (*cur->sItr)->mOfPrecursor << endl;

                    // Compute the n-terminal and c-terminal mass deviation between the peptide
                    // sequence and the spectral tag-based sequence ([200.45]NST[400.65]) 
                    // outside the tag match. For example:
                    //    XXXXXXXXNSTXXXXXXXX (peptide sequence)
                    //            |||		  (Tag match)
                    //	  [200.45]NST[400.65] (spectral tag-based sequence)
				float nTerminusDeviation = fabs( tag.nTerminusMass - cur->nTerminusMass );
				float cTerminusDeviation = fabs( tag.cTerminusMass - cur->cTerminusMass );
                    //cout << "\t\tBef:" << (nTerminusDeviation+cTerminusDeviation) << endl;
                    //if(fabs(nTerminusDeviation+cTerminusDeviation)>= g_rtConfig->MaxTagMassDeviation) {
                    //	continue;
                    //}

                    // Get the charge state of the fragment ions that gave rise to the
                    // tag
                	int tagCharge = cur->tagChargeState;
                    //cout << "\t\tAft:" << (nTerminusDeviation+cTerminusDeviation) << endl;
                    // If both mass deviations are too big then forestall the search.
                    // This is essentially searching for candidate alignments where 
                    // the tags and either of the n-terminal or c-terminal mass have to 
                    // match between the candidate petpide sequence and the tag-based
                    // sequence. This strategy only accounts for peptides where mods happen
                    // on only one side of the tag match. What happens if the residues 
                    // on both sides of the tag match are modified?
                    //cout << "\t\ttagCharge:" << tagCharge << "\n" << endl;
                    if( nTerminusDeviation > g_rtConfig->NTerminalMassTolerance[tagCharge-1] &&
                        cTerminusDeviation > g_rtConfig->CTerminalMassTolerance[tagCharge-1] )
                        continue;

                    // Get the mass spectrum
					spectrum = *cur->sItr;
                    // Get the spectrum charge
                    int spectrumCharge = spectrum->id.charge;
                    //cout << "\t\tComparing " << spectrum->id.source << " " << spectrum->id.index << endl;

                    //if( spectrum->id.charge != z+1 )
                    //	continue;

                    // If Tag and c-terminal masses match then try to put the mod
                    // on the residues that are n-terminus to the tag match. Use the
                    // total mass difference between the candidate as the mod mass.
                    float modMass = ((float)spectrum->mOfPrecursor) - neutralMass;
           
                    // Don't bother interpreting it if the mass is less than -130.0 Da
                    // -130 is close to Trp -> Gly substitution mass.
                    //if( modMass < -130.0 ) {
                    //	continue;
                    //}

                    // Don't bother interpreting the mod mass if it's outside the user-set
                    // limits.
                    if( modMass < -1.0*g_rtConfig->MaxModificationMassMinus || modMass > g_rtConfig->MaxModificationMassPlus ) {
                        continue;
                    }

                    if(g_rtConfig->FindUnknownMods || g_rtConfig->FindSequenceVariations) {
                        // Figure out if the modification mass does nullify
                        // any dynamic mods. For example, M+16ILFEGHFK peptide
                        // can not have a modification of -16 on M. 
                        bool legitimateModMass = true;
                        // Make sure that the modification mass doesn't negate the mods already present in the peptide
                        float candidateModificationsMass = g_rtConfig->UseAvgMassOfSequences ? candidate.modifications().averageDeltaMass() : candidate.modifications().monoisotopicDeltaMass();
                        if(fabs(candidateModificationsMass-modMass)< max((float)g_rtConfig->MinModificationMass, g_rtConfig->PrecursorMassTolerance[spectrumCharge-1])) {
                            legitimateModMass = false;
                        }
                        // Get the dynamic mods
                    	const ModificationMap& dynamicMods = candidate.modifications();
                        // Step through each of the dynamic mods
                    	for(ModificationMap::const_iterator itr = dynamicMods.begin(); itr != dynamicMods.end() && legitimateModMass ; ++itr) {
                            // Compute the mod mass
                        	float residueModMass = g_rtConfig->UseAvgMassOfSequences? itr->second.averageDeltaMass() : itr->second.monoisotopicDeltaMass();
                            // Check to make sure that the unknown mod mass doesn't nullify the dynamic mod mass
                            //if(fabs(residueModMass+modMass) <= g_rtConfig->PrecursorMassTolerance[spectrumCharge-1]) {
                            if(fabs(residueModMass+modMass) <= max((float)g_rtConfig->MinModificationMass, g_rtConfig->PrecursorMassTolerance[spectrumCharge-1])) {
                                legitimateModMass = false;
                            }
                        }
                        //cout << aSequence << "," << tag.tag << endl;
                        if( legitimateModMass && fabs(modMass) > max((float)g_rtConfig->MinModificationMass, g_rtConfig->PrecursorMassTolerance[spectrumCharge-1])
                            && nTerminusDeviation > g_rtConfig->NTerminalMassTolerance[tagCharge-1] 
                        && cTerminusDeviation <= g_rtConfig->CTerminalMassTolerance[tagCharge-1]) {
                            // Get the peptide sequence on the n-terminus of the tag match
                            string nTerminus = aSequence.substr( 0, (size_t) tag.lowPeakMz );


                            // If the user configured the searches for substitutions
                            if(g_rtConfig->FindSequenceVariations) {
                                //cout << "\t\t\t\t" << candidate.sequence() << "," << tag.tag << ","  << fabs(tag.nTerminusMass-cur->nTerminusMass) << "," << fabs(tag.cTerminusMass-cur->cTerminusMass) << "," << neutralMass << "," << modMass << "," << spectrum->mOfPrecursor 
                                //    << "," << (neutralMass+modMass-spectrum->mOfPrecursor) << endl;
                                // Find the substitutions that fit the mass, generate variants and score them.
                                //comparisonDone = "Seq:Nterm";
                                numComparisonsDone += ScoreSubstitutionVariants(candidate, neutralMass, modMass, 0, 
                                    (size_t) tag.lowPeakMz-1,spectrum, 
                                    idx, sequenceIons, ionTypesToSearchFor, g_rtConfig->NTerminalMassTolerance[tagCharge-1]);
                            }

                            // If the user wants us to find unknown modifications.
                            if(g_rtConfig->FindUnknownMods) {
                                //comparisonDone = "PTM:Nterm";
                                //cout << candidate.sequence() << "," << tag.tag  << "," << fabs(tag.nTerminusMass-cur->nTerminusMass) << "," << fabs(tag.cTerminusMass-cur->cTerminusMass) << "," << neutralMass << "," << modMass << "," << spectrum->mOfPrecursor 
                                //	 << "," << (neutralMass+modMass-spectrum->mOfPrecursor) << endl;
                                numComparisonsDone += ScoreUnknownModification(candidate,neutralMass, modMass, 0, 
                                    (size_t) tag.lowPeakMz-1, spectrum, 
                                    idx, sequenceIons,	ionTypesToSearchFor);
                            }
                        }

                        if( legitimateModMass && fabs(modMass) > max((float)g_rtConfig->MinModificationMass, g_rtConfig->PrecursorMassTolerance[spectrumCharge-1])
                            && cTerminusDeviation > g_rtConfig->CTerminalMassTolerance[tagCharge-1] 
                        && nTerminusDeviation <= g_rtConfig->NTerminalMassTolerance[tagCharge-1]) {
                            // Do the same thing we did for reconciling n-terminal mass difference
                            // This time we are reconciling the c-terminal mass difference
                            string cTerminus = aSequence.substr( (size_t) tag.lowPeakMz + tag.tag.length() );

                            if(g_rtConfig->FindSequenceVariations) {
                                //comparisonDone = "Seq:Cterm";
                                //cout << "\t\t\t\t" << candidate.sequence() << "," << tag.tag  << "," << fabs(tag.nTerminusMass-cur->nTerminusMass) << "," << fabs(tag.cTerminusMass-cur->cTerminusMass) << "," << neutralMass << "," << modMass << "," << spectrum->mOfPrecursor 
                                //	<< "," << (neutralMass+modMass-spectrum->mOfPrecursor) << endl;
                                // Find the substitutions that fit the mass, generate variants and score them.
                                numComparisonsDone += ScoreSubstitutionVariants(candidate, neutralMass, modMass, (size_t) tag.lowPeakMz + tag.tag.length(), 
                                    aSequence.length()-1,	spectrum, idx, 
                                    sequenceIons, ionTypesToSearchFor, g_rtConfig->CTerminalMassTolerance[tagCharge-1]);
                            }

                            if(g_rtConfig->FindUnknownMods) {
                                //comparisonDone = "PTM:Cterm";
                                //cout << candidate.sequence() << "," << tag.tag  << "," << fabs(tag.nTerminusMass-cur->nTerminusMass) << "," << fabs(tag.cTerminusMass-cur->cTerminusMass) << "," << neutralMass << "," << modMass << "," << spectrum->mOfPrecursor 
                                //	<< "," << (neutralMass+modMass-spectrum->mOfPrecursor) << endl;
                                numComparisonsDone += ScoreUnknownModification(candidate,neutralMass, modMass, (size_t) tag.lowPeakMz + tag.tag.length(), 
                                    aSequence.length()-1, spectrum, idx, 
                                    sequenceIons, ionTypesToSearchFor);
                            } 
                        } 
                    }

                    //cout << "\t\t\t" <<modMass << "," << nTerminusDeviation << "," << cTerminusDeviation << endl;
                    if(fabs(modMass) <= g_rtConfig->PrecursorMassTolerance[spectrumCharge-1] 
                    && nTerminusDeviation <= g_rtConfig->NTerminalMassTolerance[tagCharge-1]
                    && cTerminusDeviation <= g_rtConfig->CTerminalMassTolerance[tagCharge-1]) {
                        // If there are no n-terminal and c-terminal delta mass differences then
                        // score the match as an unmodified sequence.
                        //comparisonDone = "DIRECT";
                        //comparisonDone = comparisonDone + "->" + aSequence;
                        //cout << "Direct" << endl;
                        //if(matchedSpectra.find(spectrum->id)!=matchedSpectra.end()) {
                        //    continue;
                        //}
                        CalculateSequenceIons( candidate, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
                        //CalculateSequenceIons( candidate, aSequence, spectrum->id.charge, &sequenceIons, g_rtConfig->UseSmartPlusThreeModel, 0, 0, ionTypesToSearchFor );
                        spectrum->ScoreSequenceVsSpectrum( result, aSequence, sequenceIons );
                        //result.mass = neutralMass;
                        //result.mod = modMass;
                        //result.massError = spectrum->mOfPrecursor-result.mass;
                        result.massError = spectrum->mOfPrecursor-neutralMass;
                        result.lociByIndex.insert( ProteinLocusByIndex( idx + g_rtConfig->ProteinIndexOffset, candidate.offset() ) );	
                        ++numComparisonsDone;
                        //cout << numComparisonsDone << endl;
                        //comparisonDone = comparisonDone + "->" + result.sequence;
                        if( g_rtConfig->UseMultipleProcessors )
                            simplethread_lock_mutex( &spectrum->mutex );
                        if( proteins[idx].isDecoy() )
                            ++ spectrum->numDecoyComparisons;
                        else
                            ++ spectrum->numTargetComparisons;
                        spectrum->resultSet.add( result );

                        //matchedSpectra.insert(spectrum->id);
                        //cout << " " << aSequence << " " << spectrum->id.id << " " << result.getTotalScore() << " " \
                        << result.sequence << " " << result.mass << endl;
                        //cout << "\t\t\t" << aSequence << result.getTotalScore() << endl;
                        if( g_rtConfig->UseMultipleProcessors )
                            simplethread_unlock_mutex( &spectrum->mutex );
                    }
                }
            }

            //if(comparisonDone.length()>0) {
            //	cout << comparisonDone <<  endl;
            //}
            //cout << numComparisonsDone;
            //cout << comparisonDone << endl;
            return numComparisonsDone;
        }

        /**!
        ExecuteSearchThread function takes a thread, figures out which part of the protein database
        needs to be searched with the thread, generates the peptide sequences for the candidate 
        protein sequences, and searches them with the tags and the spectra that generated the tags.
        */
        simplethread_return_t ExecuteSearchThread( simplethread_arg_t threadArg )
        {
            // Get a sempahore on this function
            simplethread_lock_mutex( &resourceMutex );
            // Get the thread ID
            simplethread_id_t threadId = simplethread_get_id();
            WorkerThreadMap* threadMap = (WorkerThreadMap*) threadArg;
            // Find the data structure that is supposed to store the thread information.
            WorkerInfo* threadInfo = reinterpret_cast< WorkerInfo* >( threadMap->find( threadId )->second );
            int numThreads = (int) threadMap->size();
            // Realease the semaphore
            simplethread_unlock_mutex( &resourceMutex );

            bool done;
            //threadInfo->spectraResults.resize( (int) spectra.size() );

            Timer searchTime;
            float totalSearchTime = 0;
            float lastUpdate = 0;
            searchTime.Begin();

            while( true )
            {
                // Get the semaphore
                simplethread_lock_mutex( &resourceMutex );
                done = workerNumbers.empty();
                // If we are not done then get a worker ID
                if( !done )
                {
                    threadInfo->workerNum = workerNumbers.back();
                    workerNumbers.pop_back();
                }
                simplethread_unlock_mutex( &resourceMutex );

                if( done )
                    break;

                // Figure out which section of the protein database
                // needs to be searched with this thread
                int numProteins = (int) proteins.size();
                threadInfo->endIndex = ( numProteins / g_numWorkers )-1;

                int i;
                // The database search schema works in the following fashion
                // A protein database is split into equal parts using number
                // of processors available for search. If each processor is a
                // multi-core CPU then multiple threads (equal to number of cores
                // in a processor are created per processor. Each processor takes
                // its chunk of protein database and searches it with all the
                // spectra in the dataset using threads. The thread works in a
                // interweaving fashion.
                // For each worker thread on the processor
                for( i = threadInfo->workerNum; i < numProteins; i += g_numWorkers )
                {
                    ++ threadInfo->stats.numProteinsDigested;

                    // Get a protein sequence
                    Peptide protein(proteins[i].getSequence());
                    // Digest the protein sequence using pwiz library. The sequence is
                    // digested using cleavage rules specified in the user configuration
                    // file.
                    Digestion digestion( protein, g_rtConfig->cleavageAgentRegex, g_rtConfig->digestionConfig );
                    // For each peptide
                    for( Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); ++itr ) {
                        // Get the mass
                        double mass = g_rtConfig->UseAvgMassOfSequences ? itr->molecularWeight()
                            : itr->monoisotopicMass();
                        if( mass < g_rtConfig->curMinSequenceMass-g_rtConfig->MaxModificationMassMinus ||
                            mass > g_rtConfig->curMaxSequenceMass+g_rtConfig->MaxModificationMassPlus )
                            continue;

                        vector<DigestedPeptide> digestedPeptides;
                        START_PROFILER(12);
						
						PTMVariantList variantIterator( (*itr), g_rtConfig->MaxDynamicMods, g_rtConfig->dynamicMods, g_rtConfig->staticMods, g_rtConfig->MaxNumPeptideVariants);
                        if(variantIterator.isSkipped) {
                            ++ threadInfo->stats.numCandidatesSkipped;
                            STOP_PROFILER(12);
                            continue;
                        }
                        STOP_PROFILER(12);
                        variantIterator.getVariantsAsList(digestedPeptides);
                        threadInfo->stats.numCandidatesGenerated += digestedPeptides.size();
                        for( size_t j=0; j < digestedPeptides.size(); ++j )
                        {
                            //++ threadInfo->stats.numCandidatesGenerated;
                            START_PROFILER(1);
                            boost::int64_t queryComparisonCount = QuerySequence( digestedPeptides[j], i, g_rtConfig->EstimateSearchTimeOnly );
                            STOP_PROFILER(1);
                            if( queryComparisonCount > 0 )
                            {
                                threadInfo->stats.numComparisonsDone += queryComparisonCount;
                                ++threadInfo->stats.numCandidatesQueried;
                                //cout << "QC>0" << queryComparisonCount << endl;
                            }
                        }

                        START_PROFILER(11);
                        ++itr;
                        STOP_PROFILER(11);

                    }

                    if( g_numChildren == 0 )
                        totalSearchTime = searchTime.TimeElapsed();
                    //if( g_pid == 1 && !(i%50) )
                    //	cout << i << ": " << (int) sequenceToSpectraMap.size() << " " << (int) sequenceProteinLoci.size() << endl;

                    // Print out some numbers to keep the user interested or disinterested.....
                    if( g_numChildren == 0 && ( ( totalSearchTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || i == numProteins ) )
                    {
                        //int curProtein = ( i + 1 ) / g_numWorkers;
                        int proteinsPerSec = (int) ((float( threadInfo->stats.numProteinsDigested ) / totalSearchTime)+0.5);
                        float estimatedTimeRemaining = float( ( numProteins / numThreads ) - threadInfo->stats.numProteinsDigested ) / proteinsPerSec;

                        simplethread_lock_mutex( &resourceMutex );
                        cout << std::setprecision(1) << threadInfo->workerHostString << " has searched " << threadInfo->stats.numProteinsDigested << " of " <<	numProteins <<
                            " proteins; " << proteinsPerSec << " per second, " << totalSearchTime << " elapsed, " << estimatedTimeRemaining << " remaining." << endl;

                        PRINT_PROFILERS(cout, threadInfo->workerHostString + " profiling")

                            //float candidatesPerSec = threadInfo->stats.numComparisonsDone / totalSearchTime;
                            //float estimatedTimeRemaining = float( numCandidates - threadInfo->stats.numComparisonsDone ) / candidatesPerSec / numThreads;
                            //cout << threadInfo->workerHostString << " has made " << threadInfo->stats.numComparisonsDone << " of about " << numCandidates << " comparisons; " <<
                            //		candidatesPerSec << " per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
                            simplethread_unlock_mutex( &resourceMutex );

                        lastUpdate = totalSearchTime;
                        //cout << g_hostString << " has scored " << resultSpectra << " spectra and " << resultSequences << " sequences
                    }
                }
            }
            //i -= g_numWorkers;
            //cout << threadInfo->workerHostString << " last searched protein " << i-1 << " (" << proteins[i].name << ")." << endl;

            return 0;
        }


        /**!
        ExecuteSearch function determines the number of workers depending
        on the number of CPUs present in a box and create threads for each
        of the CPU. This function treats each multi-core CPU as a multi-processor
        machine.
        */
        searchStats ExecuteSearch()
        {
            // A map to store worker threads
            WorkerThreadMap workerThreads;

            int numProcessors = g_numWorkers;
            workerNumbers.clear();

            // If we are using a multi-core CPU or multiple processor box
            if( /*!g_singleScanMode &&*/ g_rtConfig->UseMultipleProcessors && g_numWorkers > 1 )
            {
                g_numWorkers *= g_rtConfig->ThreadCountMultiplier;

                simplethread_handle_array_t workerHandles;

                // Generate handles for each worker
                for( int i=0; i < g_numWorkers; ++i ) {
                    workerNumbers.push_back(i);
                }

                // Get a semaphore
                simplethread_lock_mutex( &resourceMutex );
                // Create a thread for each of the processor and
                // attach the procedure that needs to be executed
                // for each thread [i.e. the start() function].
                for( int t = 0; t < numProcessors; ++t )
                {
                    simplethread_id_t threadId;
                    simplethread_handle_t threadHandle = simplethread_create_thread( &threadId, &ExecuteSearchThread, &workerThreads );
                    workerThreads[ threadId ] = new WorkerInfo( t, 0, 0 );
                    workerHandles.array.push_back( threadHandle );
                }
                simplethread_unlock_mutex( &resourceMutex );

                simplethread_join_all( &workerHandles );

                // Number of worker threads is equal to number of processors
                g_numWorkers = numProcessors;
                //cout << g_hostString << " searched " << numSearched << " proteins." << endl;
            } else
            {
                // Obvious otherwise
                g_numWorkers = 1;
                workerNumbers.push_back(0);
                simplethread_id_t threadId = simplethread_get_id();
                workerThreads[ threadId ] = new WorkerInfo( 0, 0, 0 );
                ExecuteSearchThread( &workerThreads );
                //cout << g_hostString << " searched " << numSearched << " proteins." << endl;
            }

            searchStats stats;

            // Accumulate the statistics for each of the worker thread
            for( WorkerThreadMap::iterator itr = workerThreads.begin(); itr != workerThreads.end(); ++itr )
                stats = stats + reinterpret_cast< WorkerInfo* >( itr->second )->stats;

            return stats;
        }


        /**!
        ProcessHandler function reads the input files and protein database to perform the search.
        The function preprocess the spectra, splits the protein database according to the number
        of processors available (including clusters), and executes the search on each of the
        processor simultaneously. The results are put together if the search has been executed on
        a multi-core CPU or multi-node cluster.
        */
        int ProcessHandler( int argc, char* argv[] )
        {
            simplethread_create_mutex( &resourceMutex );

            //Process the command line arguments
            vector< string > args;
            for(int i=0; i < argc; ++i )
                args.push_back( argv[i] );

            if( InitProcess( args ) )
                return 1;

            //Get the database file name
            g_dbFilename = g_rtConfig->ProteinDatabase;
            //cout << g_dbFilename << "\n";
            int numSpectra = 0;

            INIT_PROFILERS(13)

            //If the process is a master process
            if( g_pid == 0 )
            {
                // Read the the regular exp and locate the input files.
                for( size_t i=1; i < args.size(); ++i )
                {
                    //cout << g_hostString << " is reading spectra from files matching mask \"" << args[i] << "\"" << endl;
                    FindFilesByMask( args[i], g_inputFilenames );
                }

                if( g_inputFilenames.empty() )
                {
                    cout << g_hostString << " did not find any spectra matching given filemasks." << endl;
                    return 1;
                }

                // Test for the protein database format
                if( !TestFileType( g_dbFilename, "fasta" ) )
                    return 1;

                cout << g_hostString << " is reading \"" << g_dbFilename << "\"" << endl;
                Timer readTime(true);
                // Read the protein database 
                try
                {
                    proteins.readFASTA( g_dbFilename, g_rtConfig->StartProteinIndex, g_rtConfig->EndProteinIndex );
                } catch( exception& e )
                {
                    cout << g_hostString << " had an error: " << e.what() << endl;
                    return 1;
                }
                cout << g_hostString << " read " << proteins.size() << " proteins; " << readTime.End() << " seconds elapsed." << endl;

                proteins.random_shuffle(); // randomize order to optimize work distribution

                // Split the database into multiple parts to distrubute it over the cluster
        #ifdef USE_MPI
                if( g_numChildren > 0 )
                {
                    g_rtConfig->ProteinBatchSize = (int) ceil( (float) proteins.size() / (float) g_numChildren / (float) g_rtConfig->NumBatches );
                    cout << g_hostString << " calculates dynamic protein batch size is " << g_rtConfig->ProteinBatchSize << endl;
                }
        #endif

                // Read the input spectra
                set<std::string> finishedFiles;
                set<std::string>::iterator fItr;
                // For each input file name
                for( fItr = g_inputFilenames.begin(); fItr != g_inputFilenames.end(); ++fItr )
                {
                    Timer fileTime(true);

                    // Clear the spectra object
                    spectra.clear();

                    cout << g_hostString << " is reading spectra from file \"" << *fItr << "\"" << endl;
                    finishedFiles.insert( *fItr );

                    Timer readTime(true);

                    //long long memoryUsageCap = (int) GetAvailablePhysicalMemory() / 4;
                    //int peakCountCap = (float) memoryUsageCap / ( sizeof( peakPreInfo ) + sizeof( peakInfo ) );
                    //cout << g_hostString << " sets memory usage ceiling at " << memoryUsageCap << ", or about " << peakCountCap << " total peaks." << endl;

                    // Read runtime variables and source data filepath from tags file (but not the tags)
                    RunTimeVariableMap varsFromFile(	"NumChargeStates DynamicMods StaticMods UseAvgMassOfSequences "
                        "DuplicateSpectra UseChargeStateFromMS PrecursorMzTolerance "
                        "FragmentMzTolerance TicCutoffPercentage" );
                    // Read the tags and return the source file path
                    string sourceFilepath;
                    if( g_rtConfig->MassReconMode )
                        // Treat input files as peak files
                        sourceFilepath = *fItr;
                    else
                        // Treat input files as tag files:
                        // Read the metadata without the tags (returns the path to peaks file, i.e. mzML)
                        sourceFilepath = spectra.readTags( *fItr, g_rtConfig->StartSpectraScanNum, g_rtConfig->EndSpectraScanNum, true);

                    // Set the parameters for the search
                    g_rtConfig->setVariables( varsFromFile );

                    // Read peaks from the source data of the tags file
                    spectra.readPeaks( sourceFilepath, g_rtConfig->StartSpectraScanNum, g_rtConfig->EndSpectraScanNum );

                    // Count total number of peaks in all the spectra in the current file
                    int totalPeakCount = 0;
                    for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
                        totalPeakCount += (*sItr)->peakCount;

                    cout << g_hostString << " read " << spectra.size() << " spectra with " << totalPeakCount << " peaks; " << readTime.End() << " seconds elapsed." << endl;

                    // Skip empty spectra
                    int skip = 0;
                    if( spectra.empty() )
                    {
                        cout << g_hostString << " is skipping a file with no spectra." << endl;
                        skip = 1;
                    }

                    // If the program is running on a cluster then determine
                    // the optimal batch size for sending the spectra over
                    // to the other processors
                #ifdef USE_MPI
                    if( g_numChildren > 0 )
                    {
                        g_rtConfig->SpectraBatchSize = (int) ceil( (float) spectra.size() / (float) g_numChildren / (float) g_rtConfig->NumBatches );
                        cout << g_hostString << " calculates dynamic spectra batch size is " << g_rtConfig->SpectraBatchSize << endl;
                    }

                    // Send the skip variable to all child processes
                    for( int p=0; p < g_numChildren; ++p )
                        MPI_Ssend( &skip,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );
               #endif

                    Timer searchTime;
                    string startTime;
                    string startDate;
                    // A data structure to store search statistics
                    searchStats sumSearchStats;
                    vector< size_t > opcs; // original peak count statistics
                    vector< size_t > fpcs; // filtered peak count statistics

                    if( !skip )
                    {
                        // If the current process is a parent process
                        if( g_numProcesses > 1 && !g_rtConfig->EstimateSearchTimeOnly )
                        {
                #ifdef USE_MPI
                            //Use the child processes to prepare the spectra
                            cout << g_hostString << " is sending spectra to worker nodes to prepare them for search." << endl;
                            Timer prepareTime(true);
                            TransmitUnpreparedSpectraToChildProcesses();

                            spectra.clear();

                            ReceivePreparedSpectraFromChildProcesses();

                            numSpectra = (int) spectra.size();

                            skip = 0;
                            if( numSpectra == 0 )
                            {
                                cout << g_hostString << " is skipping a file with no suitable spectra." << endl;
                                skip = 1;
                            }

                            // Send the message to skip the file to all sub-processes
                            for( int p=0; p < g_numChildren; ++p )
                                MPI_Ssend( &skip,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );

                            if( !skip )
                            {
                                // Print out some spectra stats
                                opcs = spectra.getOriginalPeakCountStatistics();
                                fpcs = spectra.getFilteredPeakCountStatistics();
                                cout << g_hostString << ": mean original (filtered) peak count: " <<
                                    opcs[5] << " (" << fpcs[5] << ")" << endl;
                                cout << g_hostString << ": min/max original (filtered) peak count: " <<
                                    opcs[0] << " (" << fpcs[0] << ") / " << opcs[1] << " (" << fpcs[1] << ")" << endl;
                                cout << g_hostString << ": original (filtered) peak count at 1st/2nd/3rd quartiles: " <<
                                    opcs[2] << " (" << fpcs[2] << "), " <<
                                    opcs[3] << " (" << fpcs[3] << "), " <<
                                    opcs[4] << " (" << fpcs[4] << ")" << endl;

                                float filter = 1.0f - ( (float) fpcs[5] / (float) opcs[5] );
                                cout << g_hostString << " filtered out " << filter * 100.0f << "% of peaks." << endl;

                                cout << g_hostString << " has " << spectra.size() << " spectra prepared now; " << prepareTime.End() << " seconds elapsed." << endl;
                                cout << g_hostString << " is reading tags for " << spectra.size() << " prepared spectra." << endl;
                                size_t totalTags = 0;
                                // Read the tags in the input file
                                spectra.readTags( *fItr, g_rtConfig->StartSpectraScanNum, g_rtConfig->EndSpectraScanNum, false);
                                // For each spectrum in the input file
                                for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
                                {
                                    // Get number of tags in the spectrum
                                    (*sItr)->tagList.max_size( g_rtConfig->MaxTagCount );
                                    // For each tag
                                    for( TagList::iterator tItr = (*sItr)->tagList.begin(); tItr != (*sItr)->tagList.end(); ++tItr ) {
                                        // Replace the I/L and generate new tags
                                        (*sItr)->tagList.tagExploder( *tItr );
                                    }
                                    totalTags += (*sItr)->tagList.size();
                                }
                                cout << g_hostString << " finished reading " << totalTags << " tags." << endl;

                                cout << g_hostString << " is trimming spectra with no tags." << endl;
                                int noTagsCount = spectra.trimByTagCount();
                                cout << g_hostString << " trimmed " << noTagsCount << " spectra." << endl;
                                // Initialize few global data structures. See function documentation
                                // for details
                                InitWorkerGlobals();

                                SpectraList finishedSpectra;
                                //do
                                {
                                    cout << g_hostString << " is sending some prepared spectra to all worker nodes from a pool of " << spectra.size() << " spectra." << endl;
                                    Timer sendTime(true);
                                    // Send spectra to the child processes
                                    numSpectra = TransmitSpectraToChildProcesses();
                                    cout << g_hostString << " is finished sending " << numSpectra << " prepared spectra to all worker nodes; " <<
                                        sendTime.End() << " seconds elapsed." << endl;

                                    cout << g_hostString << " is commencing database search on " << numSpectra << " spectra." << endl;
                                    startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
                                    // Send proteins to the child processes
                                    TransmitProteinsToChildProcesses();
                                    cout << g_hostString << " has finished database search; " << searchTime.End() << " seconds elapsed." << endl;

                                    cout << g_hostString << " is receiving search results for " << numSpectra << " spectra." << endl;
                                    Timer receiveTime(true);
                                    // Get the results
                                    ReceiveResultsFromChildProcesses(sumSearchStats);
                                    cout << g_hostString << " is finished receiving search results; " << receiveTime.End() << " seconds elapsed." << endl;

                                    cout << g_hostString << " overall stats: " << (string) sumSearchStats << endl;

                                    //SpectraList::iterator lastSpectrumItr = spectra.begin();
                                    //advance_to_bound( lastSpectrumItr, spectra.end(), numSpectra );
                                    //finishedSpectra.insert( spectra.begin(), spectra.end(), finishedSpectra.end() );
                                    //spectra.erase( spectra.begin(), spectra.end(), false );
                                }// while( spectra.size() > 0 );

                                //finishedSpectra.clear();

                                DestroyWorkerGlobals();
                            }

               #endif
                        } else
                        {
                            // If total number of process is <=1 then we are executing in non-cluster mode
                            cout << g_hostString << " is preparing " << spectra.size() << " spectra." << endl;
                            Timer prepareTime(true);
                            // Preprocess the spectra
                            PrepareSpectra();
                            cout << g_hostString << " is finished preparing spectra; " << prepareTime.End() << " seconds elapsed." << endl;

                            // Get number of spectra that survived the preprocessing ;-)
                            numSpectra = (int) spectra.size();

                            skip = 0;
                            if( spectra.empty() )
                            {
                                cout << g_hostString << " is skipping a file with no suitable spectra." << endl;
                                skip = 1;
                            }

                            if( !skip )
                            {
                                // If the data file has some spectra and if the process is being
                                // run on a single node then perform the search

                                // Some stats for the user!!
                                opcs = spectra.getOriginalPeakCountStatistics();
                                fpcs = spectra.getFilteredPeakCountStatistics();
                                cout << g_hostString << ": mean original (filtered) peak count: " <<
                                    opcs[5] << " (" << fpcs[5] << ")" << endl;
                                cout << g_hostString << ": min/max original (filtered) peak count: " <<
                                    opcs[0] << " (" << fpcs[0] << ") / " << opcs[1] << " (" << fpcs[1] << ")" << endl;
                                cout << g_hostString << ": original (filtered) peak count at 1st/2nd/3rd quartiles: " <<
                                    opcs[2] << " (" << fpcs[2] << "), " <<
                                    opcs[3] << " (" << fpcs[3] << "), " <<
                                    opcs[4] << " (" << fpcs[4] << ")" << endl;

                                float filter = 1.0f - ( (float) fpcs[5] / (float) opcs[5] );
                                cout << g_hostString << " filtered out " << filter * 100.0f << "% of peaks." << endl;

                                if( !g_rtConfig->MassReconMode )
                                {
                                    cout << g_hostString << " is reading tags for " << spectra.size() << " prepared spectra." << endl;
                                    size_t totalTags = 0;

                                    // Read the tags from the input file
                                    spectra.readTags( *fItr, g_rtConfig->StartSpectraScanNum, g_rtConfig->EndSpectraScanNum, false );
                                    // For each spectrum
                                    for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
                                    {
                                        // Get the number of tags and generate new tags for tags containing I/L
                                        (*sItr)->tagList.max_size( g_rtConfig->MaxTagCount );
                                        for( TagList::iterator tItr = (*sItr)->tagList.begin(); tItr != (*sItr)->tagList.end(); ++tItr )
                                            (*sItr)->tagList.tagExploder( *tItr );
                                        totalTags += (*sItr)->tagList.size();
                                    }
                                    cout << g_hostString << " finished reading " << totalTags << " tags." << endl;

                                    cout << g_hostString << " is trimming spectra with no tags." << endl;
                                    // Delete spectra that has no tags
                                    int noTagsCount = spectra.trimByTagCount();
                                    cout << g_hostString << " trimmed " << noTagsCount << " spectra." << endl;
                                }

                                // Initialize global data structures.
                                // Must be done after spectra charge states are determined and tags are read
                                InitWorkerGlobals();

                                cout << g_hostString << " is commencing database search on " << spectra.size() << " spectra." << endl;
                                startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
                                // Start the threads
                                sumSearchStats = ExecuteSearch();
                                cout << g_hostString << " has finished database search; " << searchTime.End() << " seconds elapsed." << endl;
                                cout << g_hostString << (string) sumSearchStats << endl;

                                // Free global variables
                                DestroyWorkerGlobals();
                            }
                        }

                        if( !skip )
                        {
                            // Generate an output file for each input file
                            WriteOutputToFile( *fItr, startTime, startDate, searchTime.End(), opcs, fpcs, sumSearchStats );
                            cout << g_hostString << " finished file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
                        }
                    }

        #ifdef USE_MPI
                    // Send a message to all processes about the number of files that
                    // have been processed
                    int done = ( ( g_inputFilenames.size() - finishedFiles.size() ) == 0 ? 1 : 0 );
                    for( int p=0; p < g_numChildren; ++p )
                        MPI_Ssend( &done,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );
        #endif
                }
            }
        #ifdef USE_MPI
            // If the process is a not a root process and number of child processes is
            // greater 0 then this a child process.
            else
            {
                // When executing in cluster mode
                int allDone = 0;

                // Check to make sure all the child processes are done.
                while( !allDone )
                {
                    int skip;
                    MPI_Recv( &skip,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );

                    // If we have spectra in the input file of the root process
                    if( !skip )
                    {
                        // Get unprepared spectra from root process
                        SpectraList preparedSpectra;

                        while( ReceiveUnpreparedSpectraBatchFromRootProcess() )
                        {
                            // Preprocess them
                            PrepareSpectra();
                            preparedSpectra.insert( spectra.begin(), spectra.end(), preparedSpectra.end() );
                            spectra.clear( false );
                        }
                        //cout << "totalSpectraReceived:" << preparedSpectra.size() << endl;

                        //for( int i=0; i < (int) preparedSpectra.size(); ++i )
                        //	cout << preparedSpectra[i]->id.index << " " << preparedSpectra[i]->peakData.size() << endl;

                        // Send them back
                        TransmitPreparedSpectraToRootProcess( preparedSpectra );

                        preparedSpectra.clear();

                        // See if we have to do the search
                        MPI_Recv( &skip,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );

                        // If the root process tell you to perform the search
                        if( !skip )
                        {

                            int done = 0;
                            do
                            {
                                // Get the processed spectra from the parent.
                                done = ReceiveSpectraFromRootProcess();

                                // Initialize the global variables
                                InitWorkerGlobals();

                                int numBatches = 0;
                                searchStats sumSearchStats;
                                searchStats lastSearchStats;
                                // Get a batch of protein sequences from root process
                                while( ReceiveProteinBatchFromRootProcess( lastSearchStats.numComparisonsDone ) )
                                {
                                    ++ numBatches;

                                    // Execute the search
                                    lastSearchStats = ExecuteSearch();
                                    sumSearchStats = sumSearchStats + lastSearchStats;
                                    proteins.clear();
                                }

                                cout << g_hostString << " stats: " << numBatches << " batches; " << (string) sumSearchStats << endl;

                                // Send results back to the parent process
                                TransmitResultsToRootProcess(sumSearchStats);

                                // Clean up the variables.
                                DestroyWorkerGlobals();
                                spectra.clear();
                            } while( !done );
                        }
                    }

                    // See if we are all done. Master process sends this signal when there are
                    // no more spectra to search
                    MPI_Recv( &allDone,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );
                }
            }
        #endif

            return 0;
        }
}
}
int main( int argc, char* argv[] )
{
    char buf[256];
    GetHostname( buf, sizeof(buf) );

    // Initialize the message passing interface for the parallel processing system
	#ifdef MPI_DEBUG
    cout << buf << " is initializing MPI... " << endl;
	#endif

	#ifdef USE_MPI
    int threadLevel;
    MPI_Init_thread( &argc, &argv, MPI_THREAD_MULTIPLE, &threadLevel );
    if( threadLevel < MPI_THREAD_SINGLE )
    {
        cerr << "MPI library is not thread compliant: " << threadLevel << " should be " << MPI_THREAD_MULTIPLE << endl;
        return 1;
    }
    MPI_Buffer_attach( malloc( MPI_BUFFER_SIZE ), MPI_BUFFER_SIZE );
    //CommitCommonDatatypes();
	#endif

	#ifdef MPI_DEBUG
    cout << buf << " has initialized MPI... " << endl;
	#endif

    // Get information on the MPI environment
	#ifdef MPI_DEBUG
    cout << buf << " is gathering MPI information... " << endl;
	#endif

    // Get the number of total process and the rank the parent process
	#ifdef USE_MPI
    MPI_Comm_size( MPI_COMM_WORLD, &g_numProcesses );
    MPI_Comm_rank( MPI_COMM_WORLD, &g_pid );
	#else
    g_numProcesses = 1;
    g_pid = 0;
	#endif

    g_numChildren = g_numProcesses - 1;

    ostringstream str;
    str << "Process #" << g_pid << " (" << buf << ")";
    g_hostString = str.str();

	#ifdef MPI_DEBUG
    cout << g_hostString << " has gathered its MPI information." << endl;
	#endif


    // Process the data
	#ifndef MPI_DEBUG
    cout << g_hostString << " is starting." << endl;
	#endif

    int result = 0;
    try
    {
        result = tagrecon::ProcessHandler( argc, argv );
    } catch( exception& e )
    {
        cerr << e.what() << endl;
        result = 1;
    } catch( ... )
    {
        cerr << "Caught unspecified fatal exception." << endl;
        result = 1;
    }

	#ifdef USE_MPI
    if( g_pid == 0 && g_numChildren > 0 && result > 0 )
        MPI_Abort( MPI_COMM_WORLD, 1 );
	#endif

	#ifdef MPI_DEBUG
    cout << g_hostString << " has finished." << endl;	
	#endif

    // Destroy the message passing interface
	#ifdef MPI_DEBUG
    cout << g_hostString << " is finalizing MPI... " << endl;
	#endif

	#ifdef USE_MPI
    int size;
    MPI_Buffer_detach( &g_mpiBuffer, &size );
    free( g_mpiBuffer );
    MPI_Finalize();
	#endif

	#ifdef MPI_DEBUG
    cout << g_hostString << " is terminating." << endl;
	#endif

    return result;
}

