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
// The Original Code is the MyriMatch search engine.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s):
//

#include "stdafx.h"
#include "pepitome.h"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/data/proteome/Version.hpp"
#include "PTMVariantList.h"
#include "svnrev.hpp"
#include "WuManber.h"
#include "boost/tuple/tuple.hpp"

namespace freicore
{
    namespace pepitome
    {
        WorkerThreadMap					g_workerThreads;
        simplethread_mutex_t			resourceMutex;

        vector< double>					relativePeakCount;
        vector< simplethread_mutex_t >	spectraMutexes;

        proteinStore					proteins;
        SpectraStore                    librarySpectra;
        SpectraList						spectra;
        SpectraMassMapList				spectraMassMapsByChargeState;
        float							totalSequenceComparisons;

        RunTimeConfig*					g_rtConfig;

        int Version::Major()                {return 1;}
        int Version::Minor()                {return 0;}
        int Version::Revision()             {return SVN_REV;}
        string Version::LastModified()      {return SVN_REVDATE;}
        string Version::str()               
        {
            std::ostringstream v;
            v << Major() << "." << Minor() << "." << Revision();
            return v.str();
        }

        int InitProcess( argList_t& args )
        {
            //cout << g_hostString << " is initializing." << endl;
            if( g_pid == 0 )
            {
                cout << "Pepitome " << Version::str() << " (" << Version::LastModified() << ")\n" <<
                    "FreiCore " << freicore::Version::str() << " (" << freicore::Version::LastModified() << ")\n" <<
                    "ProteoWizard MSData " << pwiz::msdata::Version::str() << " (" << pwiz::msdata::Version::LastModified() << ")\n" <<
                    "ProteoWizard Proteome " << pwiz::proteome::Version::str() << " (" << pwiz::proteome::Version::LastModified() << ")\n" <<
                    PEPITOME_LICENSE << endl;
            }

            g_rtConfig = new RunTimeConfig;
            g_rtSharedConfig = (BaseRunTimeConfig*) g_rtConfig;
            g_residueMap = new ResidueMap;
            g_endianType = GetHostEndianType();
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
                    g_numWorkers = atoi( args[i+1].c_str() );
                    args.erase( args.begin() + i );
                } else
                    continue;

                args.erase( args.begin() + i );
                --i;
            }

            if( g_pid == 0 )
            {
                for( size_t i=1; i < args.size(); ++i )
                {
                    if( args[i] == "-cfg" && i+1 <= args.size() )
                    {
                        if( g_rtConfig->initializeFromFile( args[i+1] ) )
                        {
                            cerr << g_hostString << " could not find runtime configuration at \"" << args[i+1] << "\"." << endl;
                            return 1;
                        }
                        args.erase( args.begin() + i );

                    } else if( args[i] == "-rescfg" && i+1 <= args.size() )
                    {
                        if( g_residueMap->initializeFromFile( args[i+1] ) )
                        {
                            cerr << g_hostString << " could not find residue masses at \"" << args[i+1] << "\"." << endl;
                            return 1;
                        }
                        args.erase( args.begin() + i );
                    } else
                        continue;

                    args.erase( args.begin() + i );
                    --i;
                }

                if( args.size() < 4 )
                {
                    cerr << "Not enough arguments.\nUsage: " << args[0] << " -ProteinDatabase <FASTA protein database filepath> -SpectralLibrary <spectral library path> <input spectra filemask 1> [input spectra filemask 2] ..." << endl;
                    return 1;
                }

                if( !g_rtConfig->initialized() )
                {
                    if( g_rtConfig->initializeFromFile() )
                    {
                        cerr << g_hostString << " could not find the default configuration file (hard-coded defaults in use)." << endl;
                    }
                }

                if( !g_residueMap->initialized() )
                {
                    if( g_residueMap->initializeFromFile() )
                    {
                        cerr << g_hostString << " could not find the default residue masses file (hard-coded defaults in use)." << endl;
                    }
                }

            } 
            // Command line overrides happen after config file has been distributed but before PTM parsing
            RunTimeVariableMap vars = g_rtConfig->getVariables();
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

            try
            {
                g_rtConfig->setVariables( vars );
            } catch( std::exception& e )
            {
                if( g_pid == 0 ) cerr << g_hostString << " had an error while overriding runtime variables: " << e.what() << endl;
                return 1;
            }

            if( g_rtConfig->ProteinDatabase.empty() )
            {
                cerr << "No FASTA protein database specified.\nUsage: " << args[0] << " -ProteinDatabase <FASTA protein database filepath> -SpectralLibrary <spectral library path> <input spectra filemask 1> [input spectra filemask 2] ..." << endl;
                return 1;
            }

            if( g_rtConfig->SpectralLibrary.empty() )
            {
                cerr << "No spectral library specified.\nUsage: " << args[0] << " -ProteinDatabase <FASTA protein database filepath> -SpectralLibrary <spectral library path> <input spectra filemask 1> [input spectra filemask 2] ..." << endl;
                return 1;
            }

            if( g_pid == 0 )
            {
                for( size_t i=1; i < args.size(); ++i )
                {
                    if( args[i] == "-dump" )
                    {
                        g_rtConfig->dump();
                        g_residueMap->dump();
                        args.erase( args.begin() + i );
                        --i;
                    }
                }

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

            return 0;
        }

        int InitWorkerGlobals()
        {
            spectra.sort( spectraSortByID() );

            if( spectra.empty() )
                return 0;
            // Determine the maximum seen charge state
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
                g_rtConfig->maxChargeStateFromSpectra = max((*sItr)->id.charge, g_rtConfig->maxChargeStateFromSpectra);
            g_rtConfig->maxFragmentChargeState = ( g_rtConfig->MaxFragmentChargeState > 0 ? g_rtConfig->MaxFragmentChargeState+1 : g_rtConfig->maxChargeStateFromSpectra );
            g_rtConfig->PrecursorMassTolerance.clear();
            for( int z=1; z <= g_rtConfig->maxChargeStateFromSpectra; ++z )
                g_rtConfig->PrecursorMassTolerance.push_back( g_rtConfig->PrecursorMzTolerance * z );

            // Create a map of precursor masses to the spectrum indices
            spectraMassMapsByChargeState.resize( g_rtConfig->maxChargeStateFromSpectra );
            for( int z=0; z < g_rtConfig->maxChargeStateFromSpectra; ++z )
            {
                for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
                {
                    Spectrum* s = *sItr;

                    if( s->id.charge-1 != z )
                        continue;

                    for( size_t i=0; i < s->mOfPrecursorList.size(); ++i )
                        spectraMassMapsByChargeState[z].insert( make_pair( s->mOfPrecursorList[i], sItr ) );
                    //spectraMassMapsByChargeState[z].insert( pair< float, SpectraList::iterator >( s->mOfPrecursor, sItr ) );
                    //cout << i << " " << (*sItr)->mOfPrecursor << endl;
                }
            }
            g_rtConfig->curMinSequenceMass = spectra.front()->mOfPrecursor;
            g_rtConfig->curMaxSequenceMass = 0;

            // find the smallest and largest precursor masses
            size_t maxPeakBins = (size_t) spectra.front()->totalPeakSpace;
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
            {
                if( (*sItr)->mOfPrecursor < g_rtConfig->curMinSequenceMass )
                    g_rtConfig->curMinSequenceMass = (*sItr)->mOfPrecursor;

                if( (*sItr)->mOfPrecursor > g_rtConfig->curMaxSequenceMass )
                    g_rtConfig->curMaxSequenceMass = (*sItr)->mOfPrecursor;

                double fragMassError = g_rtConfig->fragmentMzToleranceUnits == PPM ? ((*sItr)->totalPeakSpace/2.0 * g_rtConfig->FragmentMzTolerance * pow(10.0,-6)) : g_rtConfig->FragmentMzTolerance;
                size_t totalPeakBins = (size_t) round( (*sItr)->totalPeakSpace / ( fragMassError * 2.0 ) );
                if( totalPeakBins > maxPeakBins )
                    maxPeakBins = totalPeakBins;
            }
            g_rtConfig->curMinSequenceMass -= (g_rtConfig->precursorMzToleranceUnits == PPM ? (g_rtConfig->curMinSequenceMass * g_rtConfig->PrecursorMassTolerance.back() * pow(10.0,-6)) : g_rtConfig->PrecursorMassTolerance.back());
            g_rtConfig->curMaxSequenceMass += (g_rtConfig->precursorMzToleranceUnits == PPM ? (g_rtConfig->curMaxSequenceMass * g_rtConfig->PrecursorMassTolerance.back() * pow(10.0,-6)) : g_rtConfig->PrecursorMassTolerance.back());

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
            g_rtConfig->digestionConfig = Digestion::Config( g_rtConfig->NumMaxMissedCleavages,
                curMinCandidateLength,
                curMaxCandidateLength,
                specificity );

            //cout << g_hostString << " is precaching factorials up to " << (int) maxPeakBins << "." << endl;
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

            return 0;
        }

        void DestroyWorkerGlobals()
        {
        }

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

            map< int, Histogram<double> > meanScoreHistogramsByChargeState;
            for( int z=1; z <= g_rtConfig->maxChargeStateFromSpectra; ++z )
                meanScoreHistogramsByChargeState[ z ] = Histogram<double>( g_rtConfig->NumScoreHistogramBins, g_rtConfig->MaxScoreHistogramValues );

            if( g_rtConfig->CalculateRelativeScores )
            {
                Timer calculationTime(true);
                cout << g_hostString << " is calculating relative scores for " << spectra.size() << " spectra." << endl;
                float lastUpdateTime = 0;
                size_t n = 0;
                for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr, ++n )
                {
                    Spectrum* s = (*sItr);

                    try
                    {
                        s->CalculateRelativeScores();
                    } catch( std::exception& e )
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

            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
            {
                ++ numSpectra;
                Spectrum* s = (*sItr);

                spectra.setId( s->id, SpectrumId( filenameAsScanName, s->id.index, s->id.charge ) );


                s->computeSecondaryScores();

                s->resultSet.calculateRanks();
                //s->resultSet.convertProteinIndexesToNames( proteins.indexToName );
                s->resultSet.convertProteinIndexesToNames( proteins );

                if( g_rtConfig->MakeScoreHistograms )
                {
                    s->scoreHistogram.smooth();
                    for( map<double,int>::iterator itr = s->scores.begin(); itr != s->scores.end(); ++itr )
                        cout << itr->first << "\t" << itr->second << "\n";
                    //cout << std::keys( s->scores ) << endl;
                    //cout << std::values( s->scores ) << endl;
                    //cout << std::keys( s->scoreHistogram.m_bins ) << endl;
                    //cout << std::values( s->scoreHistogram.m_bins ) << endl;
                    //s->scoreHistogram.writeToSvgFile( string( s->id ) + "-histogram.svg", "MVH score", "Density", 800, 600 );
                    meanScoreHistogramsByChargeState[ s->id.charge ] += s->scoreHistogram;
                }

                for( Spectrum::SearchResultSetType::reverse_iterator itr = s->resultSet.rbegin(); itr != s->resultSet.rend(); ++itr )
                {
                    ++ numMatches;
                    numLoci += itr->lociByName.size();

                    string theSequence = itr->sequence();

                    if( itr->rank == 1 && g_rtConfig->MakeSpectrumGraphs )
                    {
                        vector< double > ionMasses;
                        vector< string > ionNames;
                        CalculateSequenceIons( Peptide(theSequence), s->id.charge, &ionMasses, s->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, &ionNames, 0 );
                        map< double, string > ionLabels;
                        map< double, string > ionColors;
                        map< double, int > ionWidths;

                        for( PeakPreData::iterator itr = s->peakPreData.begin(); itr != s->peakPreData.end(); ++itr )
                            ionWidths[ itr->first ] = 1;
                        cout << ionMasses << endl << ionNames << endl;
                        for( size_t i=0; i < ionMasses.size(); ++i )
                        {
                            double fragMassError = g_rtConfig->fragmentMzToleranceUnits == PPM ? (ionMasses[i]*g_rtConfig->FragmentMzTolerance*pow(10.0,-6)):g_rtConfig->FragmentMzTolerance;
                            PeakPreData::iterator itr = s->peakPreData.findNear( ionMasses[i], fragMassError );
                            if( itr != s->peakPreData.end() )
                            {
                                ionLabels[ itr->first ] = ionNames[i];
                                ionColors[ itr->first ] = ( ionNames[i].find( "b" ) == 0 ? "red" : "blue" );
                                ionWidths[ itr->first ] = 2;
                            }
                        }

                        cout << theSequence << " fragment ions: " << ionLabels << endl;

                        s->writeToSvgFile( string( "-" ) + theSequence + g_rtConfig->OutputSuffix, &ionLabels, &ionColors, &ionWidths );
                    }


                }
            }

            if( g_rtConfig->MakeScoreHistograms )
                for( int z=1; z <= g_rtConfig->maxChargeStateFromSpectra; ++z )
                    meanScoreHistogramsByChargeState[ z ].writeToSvgFile( filenameAsScanName + g_rtConfig->OutputSuffix + "_+" + lexical_cast<string>(z) + "_histogram.svg", "MVH score", "Density", g_rtConfig->ScoreHistogramWidth, g_rtConfig->ScoreHistogramHeight );

            RunTimeVariableMap vars = g_rtConfig->getVariables();
            RunTimeVariableMap fileParams;
            for( RunTimeVariableMap::iterator itr = vars.begin(); itr != vars.end(); ++itr )
                fileParams[ string("Config: ") + itr->first ] = itr->second;
            fileParams["SearchEngine: Name"] = "Pepitome";
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

            string outputFilename = filenameAsScanName + g_rtConfig->OutputSuffix + ".pepXML";
            cout << g_hostString << " is writing search results to file \"" << outputFilename << "\"." << endl;

            spectra.writePepXml( dataFilename, g_rtConfig->OutputSuffix, "Pepitome", g_dbPath + g_dbFilename, &proteins, fileParams );

            if( g_rtConfig->DeisotopingMode == 3 /*&& g_rtConfig->DeisotopingTestMode != 0*/ )
            {
                spectra.calculateFDRs( g_rtConfig->maxChargeStateFromSpectra, 1.0, "rev_" );
                SpectraList passingSpectra;
                spectra.filterByFDR( 0.05, &passingSpectra );
                //g_rtConfig->DeisotopingMode = g_rtConfig->DeisotopingTestMode;

                ofstream deisotopingDetails( (filenameAsScanName+g_rtConfig->OutputSuffix+"-deisotope-test.tsv").c_str() );
                deisotopingDetails << "Scan\tCharge\tSequence\tPredicted\tMatchesBefore\tMatchesAfter\n";
                for( SpectraList::iterator sItr = passingSpectra.begin(); sItr != passingSpectra.end(); ++sItr )
                {
                    Spectrum* s = (*sItr);
                    s->Deisotope( g_rtConfig->IsotopeMzTolerance );

                    s->resultSet.calculateRanks();
                    for( Spectrum::SearchResultSetType::reverse_iterator itr = s->resultSet.rbegin(); itr != s->resultSet.rend(); ++itr )
                    {
                        string theSequence = itr->sequence();

                        if( itr->rank == 1 )
                        {
                            vector< double > ionMasses;
                            CalculateSequenceIons( Peptide(theSequence), s->id.charge, &ionMasses, s->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0 );
                            int fragmentsPredicted = accumulate( itr->key.begin(), itr->key.end(), 0 );
                            int fragmentsFound = fragmentsPredicted - itr->key.back();
                            int fragmentsFoundAfterDeisotoping = 0;
                            for( size_t i=0; i < ionMasses.size(); ++i ) {
                                double fragMassError = g_rtConfig->fragmentMzToleranceUnits == PPM ? (ionMasses[i]*g_rtConfig->FragmentMzTolerance*pow(10.0,-6)):g_rtConfig->FragmentMzTolerance;
                                if( s->peakPreData.findNear( ionMasses[i], fragMassError ) != s->peakPreData.end() )
                                    ++ fragmentsFoundAfterDeisotoping;
                            }
                            deisotopingDetails << s->id.index << "\t" << s->id.charge << "\t" << theSequence << "\t" << fragmentsPredicted << "\t" << fragmentsFound << "\t" << fragmentsFoundAfterDeisotoping << "\n";
                        }
                    }
                }
                passingSpectra.clear(false);
            }

            if( g_rtConfig->AdjustPrecursorMass == 1 )
            {
                spectra.calculateFDRs( g_rtConfig->maxChargeStateFromSpectra, 1.0, "rev_" );
                SpectraList passingSpectra;
                spectra.filterByFDR( 0.05, &passingSpectra );

                ofstream adjustmentDetails( (filenameAsScanName+g_rtConfig->OutputSuffix+"-adjustment-test.tsv").c_str() );
                adjustmentDetails << "Scan\tCharge\tUnadjustedSequenceMass\tAdjustedSequenceMass\tUnadjustedPrecursorMass\tAdjustedPrecursorMass\tUnadjustedError\tAdjustedError\tSequence\n";
                for( SpectraList::iterator sItr = passingSpectra.begin(); sItr != passingSpectra.end(); ++sItr )
                {
                    Spectrum* s = (*sItr);

                    s->resultSet.calculateRanks();
                    for( Spectrum::SearchResultSetType::reverse_iterator itr = s->resultSet.rbegin(); itr != s->resultSet.rend(); ++itr )
                    {
                        if( itr->rank == 1 )
                        {
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

        void PrepareSpectra()
        {
            int numSpectra = (int) spectra.size();

            Timer timer;

            if( g_numChildren == 0 )
            {
                cout << g_hostString << " is trimming spectra with less than " << g_rtConfig->minIntensityClassCount << " peaks." << endl;
            }

            int preTrimCount = spectra.filterByPeakCount ( g_rtConfig->minIntensityClassCount );
            //int preTrimCount = spectra.filterByPeakCount( 10 );
            numSpectra = (int) spectra.size();

            if( g_numChildren == 0 )
            {
                cout << g_hostString << " trimmed " << preTrimCount << " spectra for being too sparse." << endl;
                cout << g_hostString << " is determining charge states for " << numSpectra << " spectra." << endl;
            }

            timer.Begin();
            SpectraList duplicates;
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
            {
                try
                {
                    if( !g_rtConfig->UseChargeStateFromMS )
                        spectra.setId( (*sItr)->id, SpectrumId( (*sItr)->id.index, 0 ) );

                    if( (*sItr)->id.charge == 0 )
                    {
                        SpectrumId preChargeId( (*sItr)->id );
                        (*sItr)->DetermineSpectrumChargeState();
                        SpectrumId postChargeId( (*sItr)->id );

                        if( postChargeId.charge == 0 )
                        {
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

                } catch( std::exception& e )
                {
                    throw runtime_error( string( "duplicating scan " ) + string( (*sItr)->id ) + ": " + e.what() );
                } catch( ... )
                {
                    throw runtime_error( string( "duplicating scan " ) + string( (*sItr)->id ) );
                }
            }

            try
            {
                spectra.insert( duplicates.begin(), duplicates.end(), spectra.end() );
                duplicates.clear(false);
            } catch( std::exception& e )
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
                    (*sItr)->Preprocess();
                } catch( std::exception& e )
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
                cout << g_hostString << " finished preprocessing its spectra; " << timer.End() << " seconds elapsed." << endl;
                cout << g_hostString << " is trimming spectra with less than " << g_rtConfig->minIntensityClassCount << " peaks." << endl;
                cout << g_hostString << " is trimming spectra with precursors too small or large: " <<
                    g_rtConfig->MinSequenceMass << " - " << g_rtConfig->MaxSequenceMass << endl;
            }

            int postTrimCount = spectra.filterByPeakCount( g_rtConfig->minIntensityClassCount );

            if( g_numChildren == 0 )
            {
                cout << g_hostString << " trimmed " << postTrimCount << " spectra." << endl;
            }

        }

        vector< int > workerNumbers;

        /*
         * This function takes a library spectrum index and searches it against  the experimental spectra
        */
        boost::int64_t QuerySpectrum( int libSpectrumIndex, bool estimateComparisonsOnly = false )
        {
            boost::int64_t numComparisonsDone = 0;
            Spectrum* spectrum;
            double mass = librarySpectra[libSpectrumIndex].neutralMass;
            int z = librarySpectra[libSpectrumIndex].id.charge-1;

            // Set the mass error based on mass units.
            double massError = g_rtConfig->precursorMzToleranceUnits == PPM ? (mass * g_rtConfig->PrecursorMassTolerance[z] * pow(10.0,-6)) : g_rtConfig->PrecursorMassTolerance[z];
            // Look up the spectra that have precursor masses between mass + massError and mass - massError
            SpectraMassMap::iterator begin, end;
            begin = spectraMassMapsByChargeState[z].lower_bound( mass - massError );
            end = spectraMassMapsByChargeState[z].upper_bound( mass + massError );
            if(begin == end)
            {
                return numComparisonsDone;
            }
            // Load the library spectra from the file, if we have candidate matches
            librarySpectra[libSpectrumIndex].readSpectrum();
            SearchResult result(*librarySpectra[libSpectrumIndex].matchedPeptide);
            for( ; begin != end; ++begin )
            {
                spectrum = *begin->second;

                if( !estimateComparisonsOnly )
                {

                    spectrum->ScoreSpectrumVsSpectrum(result, librarySpectra[libSpectrumIndex].peakPreData);
                    if(false) 
                    {
                        cout << spectrum->id.index << " vs " << librarySpectra[libSpectrumIndex].id.index;
                        cout << " " << spectrum->mOfPrecursor << " vs " << librarySpectra[libSpectrumIndex].neutralMass \
                            << " (" << librarySpectra[libSpectrumIndex].id.charge << ", " \
                            << fabs(spectrum->mOfPrecursor-librarySpectra[libSpectrumIndex].neutralMass) \
                            << "," << massError << ") " \
                            << "#Peaks:" << librarySpectra[libSpectrumIndex].peakPreData.size() \
                            << " " << result.mvh << ", " << result.mzFidelity << flush << endl;
                    }
                }

                ++ numComparisonsDone;

                if( estimateComparisonsOnly )
                    continue;

                START_PROFILER(4);
                if( g_rtConfig->UseMultipleProcessors )
                    simplethread_lock_mutex( &spectrum->mutex );

                /*if( proteins[idx].isDecoy() )
                ++ spectrum->numDecoyComparisons;
                else*/
                ++ spectrum->numTargetComparisons;

                if( result.mvh >= g_rtConfig->MinResultScore && accumulate( result.key.begin(), result.key.end(), 0 ) > 0 )
                {
                    result.massError = mass - begin->first;

                    if( g_rtConfig->MakeScoreHistograms )
                    {
                        ++ spectrum->scores[ result.mvh ];
                        spectrum->scoreHistogram.add( result.mvh );
                    }

                    // Accumulate score distributions for the spectrum
                    ++ spectrum->mvhScoreDistribution[ (int) (result.mvh+0.5) ];
                    ++ spectrum->mzFidelityDistribution[ (int) (result.mzFidelity+0.5)];
                    spectrum->resultSet.add( result );
                }
                if( g_rtConfig->UseMultipleProcessors )
                    simplethread_unlock_mutex( &spectrum->mutex );
                STOP_PROFILER(4);
            }
            // Clear the spectrum to keep memory in bounds.
            librarySpectra[libSpectrumIndex].clearSpectrum();

            return numComparisonsDone;
        }

        simplethread_return_t ExecuteSearchThread( simplethread_arg_t threadArg )
        {
            simplethread_lock_mutex( &resourceMutex );
            simplethread_id_t threadId = simplethread_get_id();
            WorkerThreadMap* threadMap = (WorkerThreadMap*) threadArg;
            WorkerInfo* threadInfo = reinterpret_cast< WorkerInfo* >( threadMap->find( threadId )->second );
            int numThreads = (int) threadMap->size();
            simplethread_unlock_mutex( &resourceMutex );

            bool done;
            //threadInfo->spectraResults.resize( (int) spectra.size() );

            double largestDynamicModMass = g_rtConfig->dynamicMods.empty() ? 0 : g_rtConfig->dynamicMods.rbegin()->modMass * g_rtConfig->MaxDynamicMods;
            double smallestDynamicModMass = g_rtConfig->dynamicMods.empty() ? 0 : g_rtConfig->dynamicMods.begin()->modMass * g_rtConfig->MaxDynamicMods;

            try
            {
                Timer searchTime;
                float totalSearchTime = 0;
                float lastUpdate = 0;
                searchTime.Begin();
                while( true )
                {
                    simplethread_lock_mutex( &resourceMutex );
                    done = workerNumbers.empty();
                    if( !done )
                    {
                        threadInfo->workerNum = workerNumbers.back();
                        workerNumbers.pop_back();
                    }
                    simplethread_unlock_mutex( &resourceMutex );

                    if( done )
                        break;

                    int numLibSpectra = (int) librarySpectra.size();
                    threadInfo->endIndex = ( numLibSpectra / g_numWorkers )-1;

                    int i;
                    for( i = threadInfo->workerNum; i < numLibSpectra; i += g_numWorkers )
                    {
                        ++ threadInfo->stats.numSpectraSearched;

                        boost::int64_t numComps = QuerySpectrum(i);
                        if(numComps>0)
                            ++ threadInfo->stats.numSpectraQueried;
                        threadInfo->stats.numComparisonsDone += numComps;

                        if( g_numChildren == 0 )
                            totalSearchTime = searchTime.TimeElapsed();

                        if( g_numChildren == 0 && ( ( totalSearchTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || i == numLibSpectra ) )
                        {
                            float spectraPerSec = float( threadInfo->stats.numSpectraSearched ) / totalSearchTime;
                            float estimatedTimeRemaining = float( ( numLibSpectra / numThreads ) - threadInfo->stats.numSpectraSearched ) / spectraPerSec;

                            simplethread_lock_mutex( &resourceMutex );
                            cout << threadInfo->workerHostString << " has searched " << threadInfo->stats.numSpectraSearched << " of " << numLibSpectra <<
                                " spectra; " << spectraPerSec << " per second, " << totalSearchTime << " elapsed, " << estimatedTimeRemaining << " remaining." << endl;

                            PRINT_PROFILERS(cout, threadInfo->workerHostString + " profiling")

                                simplethread_unlock_mutex( &resourceMutex );

                            lastUpdate = totalSearchTime;
                        }
                    }
                }
            } catch( std::exception& e )
            {
                cerr << threadInfo->workerHostString << " terminated with an error: " << e.what() << endl;
            } catch(...)
            {
                cerr << threadInfo->workerHostString << " terminated with an unknown error." << endl;
            }
            //i -= g_numWorkers;
            //cout << threadInfo->workerHostString << " last searched protein " << i-1 << " (" << proteins[i].name << ")." << endl;

            return 0;
        } 

        searchStats ExecuteSearch()
        {
            WorkerThreadMap workerThreads;

            int numProcessors = g_numWorkers;
            workerNumbers.clear();
            if( g_rtConfig->UseMultipleProcessors && g_numWorkers > 1 )
            {
                g_numWorkers *= g_rtConfig->ThreadCountMultiplier;

                simplethread_handle_array_t workerHandles;

                for( int i=0; i < g_numWorkers; ++i )
                    workerNumbers.push_back(i);

                simplethread_lock_mutex( &resourceMutex );
                for( int t = 0; t < numProcessors; ++t )
                {
                    simplethread_id_t threadId;
                    simplethread_handle_t threadHandle = simplethread_create_thread( &threadId, &ExecuteSearchThread, &workerThreads );
                    workerThreads[ threadId ] = new WorkerInfo( t, 0, 0 );
                    workerHandles.array.push_back( threadHandle );
                }
                simplethread_unlock_mutex( &resourceMutex );
                simplethread_join_all( &workerHandles );
                g_numWorkers = numProcessors;
                //cout << g_hostString << " searched " << numSearched << " proteins." << endl;
            } else
            {
                g_numWorkers = 1;
                workerNumbers.push_back(0);
                simplethread_id_t threadId = simplethread_get_id();
                workerThreads[ threadId ] = new WorkerInfo( 0, 0, 0 );
                ExecuteSearchThread( &workerThreads );
                //cout << g_hostString << " searched " << numSearched << " proteins." << endl;
            }

            searchStats stats;

            for( WorkerThreadMap::iterator itr = workerThreads.begin(); itr != workerThreads.end(); ++itr )
            {
                stats = stats + reinterpret_cast< WorkerInfo* >( itr->second )->stats;
                delete itr->second;
            }

            return stats;
        }

        // A tuple to hold the NTerminusIsSpecific, CTerminusIsSpecific,
        // and NTT values of a peptide.
        typedef boost::tuple<bool,bool,size_t> PeptideCleavageParams;
        // Holds the possible digestion sites in a protein
        typedef set<int> DigestionSites;

        /* This function takes a protein sequence and determines 
           the possible digestion sites in it. 
        */
        DigestionSites digestProtein(const string& sequence)
        {
            DigestionSites sites_;
            if(!g_rtConfig->cleavageAgentRegex.empty())
            {
                // Get the bounding iterators
                std::string::const_iterator start = sequence.begin();
                std::string::const_iterator end = sequence.end();
                boost::smatch what;
                boost::match_flag_type flags = boost::match_default;
                // Find a possible cleavage site from last match to the end of the sequence
                while (regex_search(start, end, what, g_rtConfig->cleavageAgentRegex, flags))
                {
                    sites_.insert(int(what[0].first-sequence.begin()-1));

                    // update search position and flags
                    start = what[0].second;
                    flags |= boost::match_prev_avail;
                    flags |= boost::match_not_bob;
                }

                // if regex didn't match n-terminus, insert it
                if (sites_.empty() || *(sites_.begin()) > -1)
                    sites_.insert(sites_.begin(), -1);

                // if regex didn't match c-terminus, insert it
                if (*(sites_.rbegin()) < (int)sequence.length()-1)
                    sites_.insert(sequence.length()-1);
            }

            return sites_;
        }

        /* This function takes a set of cleavage sites in a protein, a peptide position in
           that protein, and length of that peptide, and determines the NTT status of the peptide. */
        PeptideCleavageParams getDigestionParams(const set<int>& sites, int offset, int length)
        {
            // Get the terminal offsets
            int nTerminusOffset = offset-1;
            int cTerminusOffSet = nTerminusOffset+length;
            bool nTerminusIsSpecific = false;
            bool cTerminusIsSpecific = false;
            if(sites.find(nTerminusOffset) != sites.end())
                nTerminusIsSpecific = true;
            if(sites.find(cTerminusOffSet) != sites.end())
                cTerminusIsSpecific = true;
            // Iterate over the sites and count how 
            // many sites were missed by this peptide.
            size_t numMissCleavs = 0;
            BOOST_FOREACH(int site, sites)
                if(site > nTerminusOffset && site < cTerminusOffSet)
                    ++numMissCleavs;
            return boost::make_tuple(nTerminusIsSpecific,cTerminusIsSpecific,numMissCleavs);
        }
        
        /**
        * This funtion takes all matched peptide sequences in the library search, builds a
        * suffix tree, and scans the protein database for matches. For each protein match, 
        * we also determine the cleavage sites, and set the NTT's of the peptide in that 
        * protein appropriately.
        */
        void findProteinMatches()
        {

            typedef map<string, vector<Spectrum::SearchResultType*> > AllResults;
            typedef map<string, size_t> Matches;
            typedef const pair<int, ModificationList> ModPair;
            
            AllResults results;
            // Map peptide sequences to their corresponding results
            for(SpectraList::const_iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr)
                BOOST_FOREACH(const SearchResult& r, (*sItr)->resultSet)  
                    results[r.sequence()].push_back(&const_cast<SearchResult&>(r));
            // Use the peptide sequences as patterns for pattern-matching
            vector<const char*> patterns;
            for(AllResults::const_iterator rItr = results.begin(); rItr!= results.end(); ++rItr)
                patterns.push_back((*rItr).first.c_str());
            // Build a suffix tree with matched peptide sequences
            WuManber peptideTree;
            peptideTree.Initialize(patterns,false,false,false);
            // For each protein, search the suffix tree for matches.
            for(size_t i = 0; i < proteins.size(); ++i)
            {
                if(!(i % 1000))
                    cout << proteins.size() << ": " << i << '\r' << flush;
                // Get the cleavage sites according to user supplied cleavage rules
                DigestionSites sites_ = digestProtein(proteins[i].getSequence());
                Matches matches = peptideTree.Search(proteins[i].getSequence().length(),proteins[i].getSequence().c_str(),patterns);
                // For each match, get the corresponding results, and update their protein matches
                for(Matches::const_iterator mItr = matches.begin(); mItr != matches.end(); ++mItr)
                {
                    // Get the peptide of the match and figure out its NTT status
                    string peptide = (*mItr).first;
                    PeptideCleavageParams params = getDigestionParams(sites_, (*mItr).second, peptide.length());
                    //cout << proteins[i].getName() << "," << (*mItr).first << endl;
                    //cout << boost::get<0>(params) << "," << boost::get<1>(params) << "," << boost::get<2>(params) << endl;
                    for(vector<Spectrum::SearchResultType*>::iterator rItr = results[peptide].begin(); rItr != results[peptide].end(); ++rItr) 
                    {
                        if((*rItr)->specificTermini() > 2 || (*rItr)->missedCleavages()>10)
                        {
                            // Change NTT status of the peptide by swapping the old DigestedPeptide
                            // with new DigestedPeptide that contains appropriate NTT values.
                            DigestedPeptide& oldPep = static_cast<DigestedPeptide&>(**rItr);
                            DigestedPeptide newPep(peptide.begin(), peptide.end(),0,boost::get<2>(params),boost::get<0>(params),boost::get<1>(params));
                            ModificationMap& newPepMods = newPep.modifications();
                            BOOST_FOREACH(ModPair p, oldPep.modifications())
                                newPepMods.insert(p);
                            oldPep = newPep;
                        }
                        if((*rItr)->mvh >= g_rtConfig->MinResultScore) 
                            (*rItr)->lociByIndex.insert( ProteinLocusByIndex( i + g_rtConfig->ProteinIndexOffset, (*mItr).second ) );	
                    }
                }
            }
        }

        /*
         * This function checks each spectrum's result and purges it if it
         * does not have an associated protein.
         */
        void purgeOrphanResults()
        {
            size_t numOrphanResults = 0;
            for(SpectraList::const_iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr) {
                vector<SearchResult> resultsToPurge;
                BOOST_FOREACH(SearchResult r, (*sItr)->resultSet)
                    if(r.lociByIndex.size()==0)
                        resultsToPurge.push_back(r);
                numOrphanResults += resultsToPurge.size();
                for(vector<SearchResult>::const_iterator rItr = resultsToPurge.begin(); rItr != resultsToPurge.end(); ++rItr)
                    (*sItr)->resultSet.erase(*rItr);
            }
            if(numOrphanResults>0) 
            {
                cout << g_hostString << " " << numOrphanResults << " orphan results purged." << endl;
                cout << g_hostString << " orphan results have no associated proteins in the given FASTA database" << endl;
            }
        }

        // Shared pointer to SpectraList.
        typedef boost::shared_ptr<SpectraList> SpectraListPtr;
        /**
         * This function takes a spectra list and splits them into small batches as dictated by
         * ResultsPerBatch variable. This function also checks to make sure that the last batch
         * is not smaller than 1000 spectra.
        */
        inline vector<SpectraListPtr> estimateSpectralBatches()
        {
            int estimatedResultsSize = 0;

            // Shuffle the spectra so that there is a 
            // proper load balancing between batches.
            spectra.random_shuffle();

            vector<SpectraListPtr> batches;
            SpectraListPtr current(new SpectraList());
            // For each spectrum
            for( SpectraList::const_iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr ) 
            {
                // Check the result size, if it exceeds the limit, then push back the
                // current list into the vector and get a fresh list
                estimatedResultsSize += g_rtConfig->MaxResults;
                if(estimatedResultsSize>g_rtConfig->ResultsPerBatch) 
                {
                    batches.push_back(current);
                    current.reset(new SpectraList());
                    estimatedResultsSize = g_rtConfig->MaxResults;
                }
                current->push_back((*sItr));
            }
            // Make sure you push back the last batch
            if(current->size()>0)
                batches.push_back(current);
            // Check to see if the last batch is not a tiny batch
            if(batches.back()->size()<1000 && batches.size()>1) 
            {
                SpectraListPtr last = batches.back(); batches.pop_back();
                SpectraListPtr penultimate = batches.back(); batches.pop_back();
                penultimate->insert(last->begin(),last->end(),penultimate->end());
                batches.push_back(penultimate);
                last->clear( false );
            }
            //for(vector<SpectraListPtr>::const_iterator bItr = batches.begin(); bItr != batches.end(); ++bItr)
            //    cout << (*bItr)->size() << endl;
            return batches;
        }

        /**
         * This function is the entry point into the MyriMatch search engine. This
         * function process the command line arguments, sets up the search, triggers
         * the threads that perform the search, and writes out the result file.
        */
        int ProcessHandler( int argc, char* argv[] )
        {
            // One thread at a time please...
            simplethread_create_mutex( &resourceMutex );

            // Get the command line arguments and process them
            vector< string > args;
            for( int i=0; i < argc; ++i )
                args.push_back( argv[i] );

            if( InitProcess( args ) )
                return 1;

            // Get the database name
            g_dbFilename = g_rtConfig->ProteinDatabase;
            int numSpectra = 0;
            // Get the associated spectral library
            g_spectralLibName = g_rtConfig->SpectralLibrary;

            INIT_PROFILERS(13)

                // If this is a parent process then read the input spectral data and 
                // protein database files
                if( g_pid == 0 )
                {
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

                    if( !TestFileType( g_dbFilename, "fasta" ) )
                        return 1;

                    // Read the protein database
                    cout << g_hostString << " is reading \"" << g_dbFilename << "\"" << endl;
                    Timer readTime(true);
                    try
                    {
                        //proteins.readFASTA( g_dbFilename, g_rtConfig->StartProteinIndex, g_rtConfig->EndProteinIndex );
                        proteins.readFASTA( g_dbFilename );
                    } catch( std::exception& e )
                    {
                        cout << g_hostString << " had an error: " << e.what() << endl;
                        return 1;
                    }
                    cout << g_hostString << " read " << proteins.size() << " proteins; " << readTime.End() << " seconds elapsed." << endl;

                    // Read the associated spectral library
                    try
                    {
                        librarySpectra.loadLibraryFromMGF(g_spectralLibName);
                    } catch( std::exception& e)
                    {
                        cout << g_hostString << " had an error: " << e.what() << endl;
                        return 1;
                    }

                    // Recalcuate the precusor masses for all spectra in the library
                    cout << g_hostString << " is recalculating precursor masses for library spectra." << endl;
                    Timer recalTime(true);
                    try
                    {
                        librarySpectra.recalculatePrecursorMasses(g_rtConfig->UseAvgMassOfSequences);
                    } catch( std::exception& e)
                    {
                        cout << g_hostString << " had an error: " << e.what() << endl;
                        return 1;
                    }
                    cout << g_hostString << " finished recalcuation; " <<  recalTime.End() << " seconds elapsed." << endl;
                    // randomize order of the spectra to optimize work 
                    // distribution in the multi-threading mode.
                    librarySpectra.random_shuffle();

                    fileList_t finishedFiles;
                    fileList_t::iterator fItr;
                    // For each input spectra file
                    for( fItr = g_inputFilenames.begin(); fItr != g_inputFilenames.end(); ++fItr )
                    {
                        Timer fileTime(true);

                        // Hold the spectra objects
                        spectra.clear();
                        // Holds the map of parent mass to corresponding spectra
                        spectraMassMapsByChargeState.clear();

                        cout << g_hostString << " is reading spectra from file \"" << *fItr << "\"" << endl;
                        finishedFiles.insert( *fItr );

                        Timer readTime(true);

                        // Read the spectra
                        try
                        {
                            spectra.readPeaks( *fItr, g_rtConfig->StartSpectraScanNum, g_rtConfig->EndSpectraScanNum );
                        } catch( std::exception& e )
                        {
                            cerr << g_hostString << " had an error: " << e.what() << endl;
                            return 1;
                        }

                        // Compute the peak counts
                        int totalPeakCount = 0;
                        numSpectra = (int) spectra.size();
                        for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
                            totalPeakCount += (*sItr)->peakPreCount;

                        cout << g_hostString << " read " << numSpectra << " spectra with " << totalPeakCount << " peaks; " << readTime.End() << " seconds elapsed." << endl;

                        int skip = 0;
                        if( numSpectra == 0 )
                        {
                            cout << g_hostString << " is skipping a file with no spectra." << endl;
                            skip = 1;
                        }

                        Timer searchTime;
                        string startTime;
                        string startDate;
                        searchStats sumSearchStats;
                        vector< size_t > opcs; // original peak count statistics
                        vector< size_t > fpcs; // filtered peak count statistics

                        // If the file has spectra
                        if( !skip )
                        {
                            // prepare the spectra
                            cout << g_hostString << " is preparing " << numSpectra << " spectra." << endl;
                            Timer prepareTime(true);
                            PrepareSpectra();
                            cout << g_hostString << " is finished preparing spectra; " << prepareTime.End() << " seconds elapsed." << endl;
                            numSpectra = (int) spectra.size();

                            skip = 0;
                            if( numSpectra == 0 )
                            {
                                cout << g_hostString << " is skipping a file with no suitable spectra." << endl;
                                skip = 1;
                            }
                            // If the file has spectra to search
                            if( !skip )
                            {
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

                                InitWorkerGlobals();
                                // Start the search
                                if( !g_rtConfig->EstimateSearchTimeOnly )
                                {
                                    cout << g_hostString << " is commencing library search on " << numSpectra << " spectra." << endl;
                                    startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
                                    sumSearchStats = ExecuteSearch();
                                    cout << g_hostString << " has finished library search; " << searchTime.End() << " seconds elapsed." << endl;

                                    cout << g_hostString << " overall stats: " << (string) sumSearchStats << endl;
                                } else
                                {
                                    cout << g_hostString << " is estimating the count of spectral comparisons to be done." << endl;
                                    sumSearchStats = ExecuteSearch();
                                    double estimatedComparisonsPerProtein = sumSearchStats.numComparisonsDone / (double) sumSearchStats.numSpectraQueried;
                                    boost::int64_t estimatedTotalComparisons = (boost::int64_t) (estimatedComparisonsPerProtein * proteins.size());
                                    cout << g_hostString << " will make an estimated total of " << estimatedTotalComparisons << " spectral comparisons." << endl;
                                    skip = 1;
                                }
                            }

                            DestroyWorkerGlobals();
                            // Write the output
                            if( !skip ) {
                                Timer mapTimer(true);
                                cout << g_hostString << " mapping library peptide matches to fasta database." << endl;
                                findProteinMatches();
                                cout << g_hostString << " finished mapping; " << mapTimer.End() << " seconds elapsed." << endl;
                                purgeOrphanResults();
                                WriteOutputToFile( *fItr, startTime, startDate, searchTime.End(), opcs, fpcs, sumSearchStats );
                                cout << g_hostString << " finished file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
                                //PRINT_PROFILERS(cout,"old");
                            }

                        }

                    }
                }

                return 0;
        }
    }
}

int main( int argc, char* argv[] )
{
    char buf[256];
    GetHostname( buf, sizeof(buf) );

    g_numProcesses = 1;
    g_pid = 0;

    g_numChildren = g_numProcesses - 1;

    ostringstream str;
    str << "Process #" << g_pid << " (" << buf << ")";
    g_hostString = str.str();

    int result = 0;
    try
    {
        result = pepitome::ProcessHandler( argc, argv );
    } catch( std::exception& e )
    {
        cerr << e.what() << endl;
        result = 1;
    } catch( ... )
    {
        cerr << "Caught unspecified fatal exception." << endl;
        result = 1;
    }

    return result;
}
