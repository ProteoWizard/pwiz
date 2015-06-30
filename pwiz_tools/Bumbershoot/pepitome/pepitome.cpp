//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
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
#include "pwiz/utility/misc/DateTime.hpp"
#include "PTMVariantList.h"
#include "WuManber.h"
#include "LibraryBabelFish.h"
#include "boost/tuple/tuple.hpp"
#include "boost/lockfree/queue.hpp"
#include "boost/exception/all.hpp"
#include "boost/xpressive/xpressive.hpp"
#include "pepitomeVersion.hpp"

namespace freicore
{
namespace pepitome
{
    proteinStore                    proteins;
    proteinStore                    originalProteins;
    boost::lockfree::queue<size_t>  libraryTasks;
    SearchStatistics                searchStatistics;

    SpectraList                        spectra;
    SpectraMassMapList                avgSpectraByChargeState;
    SpectraMassMapList                monoSpectraByChargeState;
        
    SpectraStore                    librarySpectra;
    RunTimeConfig*                    g_rtConfig;


    int InitProcess( argList_t& args )
    {
        cout << "Pepitome " << Version::str() << " (" << Version::LastModified() << ")\n" <<
                "FreiCore " << freicore::Version::str() << " (" << freicore::Version::LastModified() << ")\n" <<
                "ProteoWizard MSData " << pwiz::msdata::Version::str() << " (" << pwiz::msdata::Version::LastModified() << ")\n" <<
                "ProteoWizard Proteome " << pwiz::proteome::Version::str() << " (" << pwiz::proteome::Version::LastModified() << ")\n" <<
                PEPITOME_LICENSE << endl;
            
        string usage = "Usage: " + lexical_cast<string>(bfs::path(args[0]).filename()) + " [optional arguments] <input spectra filemask 1> [input spectra filemask 2] ...\n"
                       "Optional arguments:\n"
                       "-cfg <config filepath>        : specify a configuration file other than the default\n"
                       "-workdir <working directory>  : change working directory (where output files are written)\n"
                       "-cpus <value>                 : force use of <value> worker threads\n"
                       "-ignoreConfigErrors           : ignore errors in configuration file or the command-line\n"
                       "-AnyParameterName <value>     : override the value of the given parameter to <value>\n"
                       "-dump                         : show runtime configuration settings before starting the run\n"
                       "-preplib                      : (Beta) append contaminants (if provided) and decoys to spectral library then exit\n";

        bool ignoreConfigErrors = false;
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
            } else if( args[i] == "-ignoreConfigErrors" )
            {
                ignoreConfigErrors = true;
            } else
                continue;

            args.erase( args.begin() + i );
            --i;
        }

        g_rtConfig = new RunTimeConfig(!ignoreConfigErrors);
        g_rtSharedConfig = (BaseRunTimeConfig*) g_rtConfig;

        for( size_t i=1; i < args.size(); ++i )
        {
            if( args[i] == "-cfg" && i+1 <= args.size() )
            {
                if( g_rtConfig->initializeFromFile( args[i+1] ) )
                {
                    cerr << "Could not find runtime configuration at \"" << args[i+1] << "\"." << endl;
                    return 1;
                }
                args.erase( args.begin() + i );

            } else
                continue;

            args.erase( args.begin() + i );
            --i;
        }

        if( args.size() < 2 )
        {
            cerr << "Not enough arguments.\n\n" << usage << endl;
            return 1;
        }

        if( !g_rtConfig->initialized() )
        {
            if( g_rtConfig->initializeFromFile() )
                cerr << "Could not find the default configuration file (hard-coded defaults in use)." << endl;
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

        g_rtConfig->setVariables( vars );

        for( size_t i=1; i < args.size(); ++i )
        {
            if( args[i] == "-dump" )
            {
                g_rtConfig->dump();
                args.erase( args.begin() + i );
                --i;
            }
        }

        bool preplib = false;
        bool iTraqMode = false;
        bool hcdMode = false;
        for( size_t i=1; i < args.size(); ++i )
        {
            if( args[i][0] == '-' )
            {
                if( args[i] == "-preplib")
                    preplib = true;                
                else if( args[i] == "-prephcd")
                {
                    preplib = true;
                    hcdMode = true;
                }
                else if( args[i] == "-itraq4plex")
                {
                    preplib = true;
                    hcdMode = true;
                    iTraqMode = true;
                }
                else if (!ignoreConfigErrors)
                {
                    cerr << "Error: unrecognized parameter \"" << args[i] << "\"" << endl;
                    return 1;
                }
                else
                    cerr << "Warning: ignoring unrecognized parameter \"" << args[i] << "\"" << endl;
                args.erase( args.begin() + i );
                --i;
            }
        }
        
        if( g_rtConfig->ProteinDatabase.empty() )
        {
            cerr << "No FASTA protein database specified.\n\n" << usage << endl;
            return 1;
        }        

        if( g_rtConfig->SpectralLibrary.empty() )
        {
            cerr << "No spectral library specified.\n\n" << usage << endl;
            return 1;
        }        
        if (preplib)
        {
            if (iTraqMode)
                cout << "Using iTRAQ library preparation mode; peak mz values will be shifted to match iTRAQ 4plex modifications" << endl;
            else if (hcdMode)
                cout << "Using HCD library preparation mode; peak mz values will be shifted to match theoretical location" << endl;
            else
                cout << "Using library preparation mode" << endl;
            if (!bfs::exists(g_rtConfig->ContamDatabase) && g_rtConfig->ContamDatabase != "")
            {
                string temp = g_rtConfig->ContamDatabase;
                //if (std::string::npos != temp.find("default"))
                boost::filesystem::path p(args[0]);
                boost::filesystem::path dir = p.parent_path();
                dir /= "contams.fasta";
                if (boost::iequals(temp, "default"))
                    g_rtConfig->ContamDatabase = dir.string();
                else
                {
                    cerr << "Specified contaminant FASTA protein database not found or cannot be read." << endl << g_rtConfig->ContamDatabase << endl << endl;
                    return 1;
                }
            }
            if (!bfs::exists(g_rtConfig->ContamLibrary) && g_rtConfig->ContamLibrary != "")
            {
                string temp = g_rtConfig->ContamLibrary;
                //if (std::string::npos != temp.find("default"))
                boost::filesystem::path p(args[0]);
                boost::filesystem::path dir = p.parent_path();
                dir /= "contams.sptxt";
                if (boost::iequals(temp, "default"))
                    g_rtConfig->ContamLibrary = dir.string();
                else
                {
                    cerr << "Specified contaminant spectral library not found or cannot be read.\n\n" << endl;
                    return 1;            
                }
            }

            //Create merged database and library
            string newDatabase = LibraryBabelFish::mergeDatabaseWithContam(g_rtConfig->ProteinDatabase, g_rtConfig->ContamDatabase);
            string newLibrary = LibraryBabelFish::mergeLibraryWithContam(g_rtConfig->SpectralLibrary, g_rtConfig->ContamLibrary);

            // Read the protein database
            cout << "Reading \"" << newDatabase << "\"" << endl;
            Timer readTime(true);
            try
            {
                originalProteins = proteinStore( g_rtConfig->DecoyPrefix );
                originalProteins.readFASTA( newDatabase );
            }
            catch (std::exception& e)
            {
                cout << "Error loading protein database: " << e.what() << endl;
                return 1;
            }
            cout << "Read " << originalProteins.size() << " proteins; " << readTime.End() << " seconds elapsed." << endl << endl;



            //refresh library against database
            {
                //LibraryBabelFish converter(newLibrary);
                LibraryBabelFish::refreshLibrary(newLibrary, originalProteins, hcdMode, iTraqMode,g_rtConfig->DecoyPrefix);
            }

            cout << endl << "Library refreshed and decoys created..." << endl;

            //Set merged database as new g_rtConfig->ProteinDatabase
            //Set decoyed library as new g_rtConfig->SpectralLibrary
            g_rtConfig->SpectralLibrary = newLibrary;
            g_rtConfig->ProteinDatabase = newDatabase;

            cout << "New Database: " + g_rtConfig->ProteinDatabase << endl;
            cout << "New Library: " + g_rtConfig->SpectralLibrary << endl << endl;

            cout << "Library preparation complete." << endl << endl;
            return 1;
            //Note: currently exit program after library prep is complete. Errors sometimes occur in searching when
            //preparation and searching is done in the same run of Pepitome. Best guess at the moment is memory allocation issue
        }

        if (args.size() == 1)
        {
            cerr << "No data sources specified.\n\n" << usage << endl;
            return 1;
        }

        return 0;
    }

    int InitWorkerGlobals()
    {
        spectra.sort( spectraSortByID() );

        if( spectra.empty() )
            return 0;

        // Determine the maximum seen charge state
        BOOST_FOREACH(Spectrum* s, spectra)
            g_rtConfig->maxChargeStateFromSpectra = max(s->possibleChargeStates.back(), g_rtConfig->maxChargeStateFromSpectra);

        g_rtConfig->maxFragmentChargeState = ( g_rtConfig->MaxFragmentChargeState > 0 ? g_rtConfig->MaxFragmentChargeState+1 : g_rtConfig->maxChargeStateFromSpectra );

        g_rtConfig->monoPrecursorMassTolerance.clear();
        g_rtConfig->avgPrecursorMassTolerance.clear();
        for( int z=1; z <= g_rtConfig->maxChargeStateFromSpectra; ++z )
        {
            g_rtConfig->monoPrecursorMassTolerance.push_back( MZTolerance(g_rtConfig->MonoPrecursorMzTolerance.value * z,
                g_rtConfig->MonoPrecursorMzTolerance.units) );
            g_rtConfig->avgPrecursorMassTolerance.push_back( MZTolerance(g_rtConfig->AvgPrecursorMzTolerance.value * z,
                g_rtConfig->AvgPrecursorMzTolerance.units) );
        }

        size_t monoPrecursorHypotheses = 0, avgPrecursorHypotheses = 0;

        // Create a map of precursor masses to the spectrum indices
        monoSpectraByChargeState.resize( g_rtConfig->maxChargeStateFromSpectra );
        avgSpectraByChargeState.resize( g_rtConfig->maxChargeStateFromSpectra );
        for( int z=0; z < g_rtConfig->maxChargeStateFromSpectra; ++z )
        {
            BOOST_FOREACH(Spectrum* s, spectra)
                BOOST_FOREACH(const PrecursorMassHypothesis& p, s->precursorMassHypotheses)
                if (p.charge != z+1) continue;
                else if (g_rtConfig->precursorMzToleranceRule == MzToleranceRule_Mono ||
                    p.massType == MassType_Monoisotopic && g_rtConfig->precursorMzToleranceRule != MzToleranceRule_Avg)
                    monoSpectraByChargeState[z].insert(make_pair(p.mass, make_pair(s, p)));
                else
                    avgSpectraByChargeState[z].insert(make_pair(p.mass, make_pair(s, p)));

                monoPrecursorHypotheses += monoSpectraByChargeState[z].size();
                avgPrecursorHypotheses += avgSpectraByChargeState[z].size();
        }

        cout << "Monoisotopic mass precursor hypotheses: " << monoPrecursorHypotheses << endl
             << "Average mass precursor hypotheses: " << avgPrecursorHypotheses << endl;

        g_rtConfig->curMinPeptideMass = spectra.front()->precursorMassHypotheses.front().mass;
        g_rtConfig->curMaxPeptideMass = 0;

        // find the smallest and largest precursor masses
        size_t maxPeakBins = (size_t) spectra.front()->totalPeakSpace;
        BOOST_FOREACH(Spectrum* s, spectra)
        {
            g_rtConfig->curMinPeptideMass = min(g_rtConfig->curMinPeptideMass, s->precursorMassHypotheses.front().mass);
            g_rtConfig->curMaxPeptideMass = max(g_rtConfig->curMaxPeptideMass, s->precursorMassHypotheses.back().mass);

            double fragMassError = g_rtConfig->FragmentMzTolerance.units == MZTolerance::PPM ? (s->totalPeakSpace/2.0 * g_rtConfig->FragmentMzTolerance.value * 1e-6) : g_rtConfig->FragmentMzTolerance.value;
            size_t totalPeakBins = (size_t) round( s->totalPeakSpace / ( fragMassError * 2.0 ) );
            if( totalPeakBins > maxPeakBins )
                maxPeakBins = totalPeakBins;
        }

        // adjust for precursor tolerance
        g_rtConfig->curMinPeptideMass -= g_rtConfig->AvgPrecursorMzTolerance;
        g_rtConfig->curMaxPeptideMass += g_rtConfig->AvgPrecursorMzTolerance;

        // adjust for DynamicMods
        g_rtConfig->curMinPeptideMass = min( g_rtConfig->curMinPeptideMass, g_rtConfig->curMinPeptideMass - g_rtConfig->largestPositiveDynamicModMass );
        g_rtConfig->curMaxPeptideMass = max( g_rtConfig->curMaxPeptideMass, g_rtConfig->curMaxPeptideMass - g_rtConfig->largestNegativeDynamicModMass );

        // adjust for user settings
        g_rtConfig->curMinPeptideMass = max( g_rtConfig->curMinPeptideMass, g_rtConfig->MinPeptideMass );
        g_rtConfig->curMaxPeptideMass = min( g_rtConfig->curMaxPeptideMass, g_rtConfig->MaxPeptideMass );

        double minResidueMass = AminoAcid::Info::record('G').residueFormula.monoisotopicMass();
        double maxResidueMass = AminoAcid::Info::record('W').residueFormula.monoisotopicMass();

        // calculate minimum length of a peptide made entirely of tryptophan over the minimum mass
        int curMinPeptideLength = max( g_rtConfig->MinPeptideLength,
            (int) floor( g_rtConfig->curMinPeptideMass /
            maxResidueMass ) );

        // calculate maximum length of a peptide made entirely of glycine under the maximum mass
        int curMaxPeptideLength = min((int) ceil( g_rtConfig->curMaxPeptideMass / minResidueMass ), 
            g_rtConfig->MaxPeptideLength);

        // set digestion parameters
        Digestion::Specificity specificity = (Digestion::Specificity) g_rtConfig->MinTerminiCleavages;
        g_rtConfig->digestionConfig = Digestion::Config( g_rtConfig->MaxMissedCleavages,
                                                            curMinPeptideLength,
                                                            curMaxPeptideLength,
                                                            specificity );

        //cout << g_hostString << " is precaching factorials up to " << (int) maxPeakBins << "." << endl;
        g_lnFactorialTable.resize( maxPeakBins );
        //cout << g_hostString << " finished precaching factorials." << endl;

        cout << "Min. effective peptide mass is " << g_rtConfig->curMinPeptideMass << endl;
        cout << "Max. effective peptide mass is " << g_rtConfig->curMaxPeptideMass << endl;
        cout << "Min. effective peptide length is " << curMinPeptideLength << endl;
        cout << "Max. effective peptide length is " << curMaxPeptideLength << endl;

        return 0;
    }

    void DestroyWorkerGlobals()
    {
    }

    void WriteOutputToFile(    const string& dataFilename,
                            string startTime,
                            string startDate,
                            float totalSearchTime,
                            vector< size_t > opcs,
                            vector< size_t > fpcs,
                            SearchStatistics& overallStats )
    {
        int numSpectra = 0;

        string filenameAsScanName = basename(bfs::path(dataFilename));
        BOOST_FOREACH(Spectrum* s, spectra)
        {
            ++ numSpectra;
            spectra.setId( s->id, SpectrumId( filenameAsScanName, s->id.nativeID, s->id.charge ) );
            s->computeSecondaryScores();
        }

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
            
        string extension = g_rtConfig->outputFormat == pwiz::identdata::IdentDataFile::Format_pepXML ? ".pepXML" : ".mzid";
        string outputFilename = filenameAsScanName + g_rtConfig->OutputSuffix + extension;
        cout << "Writing search results to file \"" << outputFilename << "\"." << endl;

        spectra.write( dataFilename, 
                        g_rtConfig->outputFormat,
                        g_rtConfig->OutputSuffix,
                        "Pepitome", 
                        Version::str(),
                        "http://forge.fenchurch.mc.vanderbilt.edu/projects/pepitome/",
                        g_dbPath + g_dbFilename,
                        g_rtConfig->cleavageAgent,
                        g_rtConfig->cleavageAgentRegex,
                        g_rtConfig->DecoyPrefix,
                        fileParams );
    }

    void PrepareSpectra()
    {
        int numSpectra = (int) spectra.size();

        Timer timer;

        cout << "Trimming spectra with less than " << g_rtConfig->minIntensityClassCount << " peaks." << endl;

        int preTrimCount = spectra.filterByPeakCount ( g_rtConfig->minIntensityClassCount );
        //int preTrimCount = spectra.filterByPeakCount( 10 );
        numSpectra = (int) spectra.size();

        cout << "Trimmed " << preTrimCount << " spectra for being too sparse." << endl;
        cout << "Preprocessing " << numSpectra << " spectra." << endl;
            
        timer.Begin();
        BOOST_FOREACH(Spectrum* s, spectra)
        {
            try
            {
                s->Preprocess();
            } catch( std::exception& e )
            {
                stringstream msg;
                msg << "preprocessing spectrum " << s->id << ": " << e.what();
                throw runtime_error( msg.str() );
            } catch( ... )
            {
                stringstream msg;
                msg << "preprocessing spectrum " << s->id;
                throw runtime_error( msg.str() );
            }
        }

        // Trim spectra that have observed precursor masses outside the user-configured range
        // (erase the peak list and the trim 0 peaks out)
        BOOST_FOREACH(Spectrum* s, spectra)
        {
            if( s->precursorMassHypotheses.back().mass < g_rtConfig->MinPeptideMass ||
                s->precursorMassHypotheses.front().mass > g_rtConfig->MaxPeptideMass )
            {
                s->peakPreData.clear();
                s->peakData.clear();
            }
        }

        cout << "Finished preprocessing its spectra; " << timer.End() << " seconds elapsed." << endl;
        cout << "Trimming spectra with less than " << g_rtConfig->minIntensityClassCount << " peaks." << endl;
        cout << "Trimming spectra with precursors too small or large: " <<
            g_rtConfig->MinPeptideMass << " - " << g_rtConfig->MaxPeptideMass << endl;

        int postTrimCount = spectra.filterByPeakCount( g_rtConfig->minIntensityClassCount );
        cout << "Trimmed " << postTrimCount << " spectra." << endl;

    }

    typedef pair<string,size_t> Protein;

    void readLibrarySpectraAsBatch(const vector<size_t>& indices, CandidateQueries& candidateQueries)
    {

        vector<size_t> spectraToBeRead;
        BOOST_FOREACH(size_t currSpectrumIndex, indices)
        {
                
            if(librarySpectra[currSpectrumIndex]->id.charge > g_rtConfig->maxChargeStateFromSpectra)
            {
                ++searchStatistics.numSpectraSearched;
                continue;
            }
            double libraryMass = librarySpectra[currSpectrumIndex]->libraryMass;
            if(libraryMass < g_rtConfig->curMinPeptideMass || libraryMass > g_rtConfig->curMaxPeptideMass)
            {
                ++searchStatistics.numSpectraSearched;
                continue;
            }
            int z = librarySpectra[currSpectrumIndex]->id.charge-1;
            // Look up the spectra that have precursor mass hypotheses between mass + massError and mass - massError
            vector<SpectraMassMap::iterator> candidateHypotheses;
            SpectraMassMap::iterator cur, end;
            if(g_rtConfig->RecalculateLibPepMasses)
            {
                double monoCalculatedMass = librarySpectra[currSpectrumIndex]->monoisotopicMass;
                double avgCalculatedMass = librarySpectra[currSpectrumIndex]->averageMass;

                end = monoSpectraByChargeState[z].upper_bound( monoCalculatedMass + g_rtConfig->monoPrecursorMassTolerance[z] );
                for( cur = monoSpectraByChargeState[z].lower_bound( monoCalculatedMass - g_rtConfig->monoPrecursorMassTolerance[z] ); cur != end; ++cur )
                    candidateHypotheses.push_back(cur);

                end = avgSpectraByChargeState[z].upper_bound( avgCalculatedMass + g_rtConfig->avgPrecursorMassTolerance[z] );
                for( cur = avgSpectraByChargeState[z].lower_bound( avgCalculatedMass - g_rtConfig->avgPrecursorMassTolerance[z] ); cur != end; ++cur )
                    candidateHypotheses.push_back(cur);
            } 
            else
            {
                end = monoSpectraByChargeState[z].upper_bound( libraryMass + g_rtConfig->monoPrecursorMassTolerance[z] );
                for( cur = monoSpectraByChargeState[z].lower_bound( libraryMass - g_rtConfig->monoPrecursorMassTolerance[z] ); cur != end; ++cur )
                    candidateHypotheses.push_back(cur);

                end = avgSpectraByChargeState[z].upper_bound( libraryMass + g_rtConfig->avgPrecursorMassTolerance[z] );
                for( cur = avgSpectraByChargeState[z].lower_bound( libraryMass - g_rtConfig->avgPrecursorMassTolerance[z] ); cur != end; ++cur )
                    candidateHypotheses.push_back(cur);
            }

            if(candidateHypotheses.size()==0)
            {
                ++searchStatistics.numSpectraSearched;
                continue;
            }

            candidateQueries.insert(make_pair(currSpectrumIndex,candidateHypotheses));
            spectraToBeRead.push_back(currSpectrumIndex);
        }
        START_PROFILER(3)
        librarySpectra.readSpectraAsBatch(spectraToBeRead);
        searchStatistics.numSpectraSearched += spectraToBeRead.size();
        STOP_PROFILER(3)
    }
        
    typedef vector< vector < size_t > > LibraryBatchTasks;
    LibraryBatchTasks libBatchValues;

    /*
        * This function takes a library spectrum index and searches it against  the experimental spectra
    */
    boost::int64_t QueryLibraryBatch( int libBatchIndex, bool estimateComparisonsOnly = false )
    {
        boost::int64_t numComparisonsDone = 0;
            
        CandidateQueries queries;
        readLibrarySpectraAsBatch(libBatchValues[libBatchIndex], queries);
        BOOST_FOREACH(const Query& query, queries)
        {
            size_t libSpectrumIndex = query.first;
            vector<SpectraMassMap::iterator> candidateHypotheses = query.second;
            // Load the library spectra from the file, if we have candidate matches
            START_PROFILER(12)
            librarySpectra[libSpectrumIndex]->preprocessSpectrum(g_rtConfig->LibTicCutoffPercentage, g_rtConfig->LibMaxPeakCount, g_rtConfig->CleanLibSpectra);
            STOP_PROFILER(12)
            int libPeptideLength = librarySpectra[libSpectrumIndex]->matchedPeptide->sequence().length();
            if(libPeptideLength < g_rtConfig->MinPeptideLength || libPeptideLength > g_rtConfig->MaxPeptideLength)
                continue;
                
            ++searchStatistics.numSpectraQueried;
            int z = librarySpectra[libSpectrumIndex]->id.charge-1;
            BOOST_FOREACH(SpectraMassMap::iterator spectrumHypothesisPair, candidateHypotheses)
            {
                ++numComparisonsDone;
                if( estimateComparisonsOnly )
                    continue;

                Spectrum* spectrum = spectrumHypothesisPair->second.first;
                PrecursorMassHypothesis& p = spectrumHypothesisPair->second.second;
                    
                boost::shared_ptr<SearchResult> resultPtr(new SearchResult(*librarySpectra[libSpectrumIndex]->matchedPeptide));
                SearchResult& result = *resultPtr;
                START_PROFILER(5)
                spectrum->ScoreSpectrumVsSpectrum(result, librarySpectra[libSpectrumIndex]->peakData);
                if (result.mvh >= g_rtConfig->MinResultScore)
                {
                    BOOST_FOREACH(const Protein& protein, librarySpectra[libSpectrumIndex]->matchedProteins)
                        result.proteins.insert(protein.first);
                }
                STOP_PROFILER(5)
                    
                START_PROFILER(4);
                {
                    boost::unique_lock<boost::mutex> guard(spectrum->mutex);

                    ++spectrum->numTargetComparisons;

                    if( result.mvh >= g_rtConfig->MinResultScore )
                    {
                        result.precursorMassHypothesis = p;
                        // Accumulate score distributions for the spectrum
                        ++ spectrum->mvhScoreDistribution[ (int) (result.mvh+0.5) ];
                        ++ spectrum->mzFidelityDistribution[ (int) (result.mzFidelity+0.5)];
                        spectrum->resultsByCharge[z].add( resultPtr );   
                    }
                }
                STOP_PROFILER(4);
            }
        }
        // Clear the spectra to keep memory in bounds
        BOOST_FOREACH(size_t index, libBatchValues[libBatchIndex])
            librarySpectra[index].reset();
        return numComparisonsDone;
    }

    struct ThreadStatus
    {
        boost::exception_ptr exception;

        ThreadStatus() {}
        ThreadStatus(const boost::exception_ptr& e) : exception(e) {}
    };

    void ExecuteSearchThread(ThreadStatus& status)
    {
        try
        {
            size_t libraryTask;
            while( true )
            {
                if (!libraryTasks.pop(libraryTask))
                    break;
                    
                boost::int64_t numComps = QueryLibraryBatch(libraryTask);
                searchStatistics.numComparisonsDone += numComps;
            }
        }
        catch (std::exception& e)
        {
            status = boost::copy_exception(runtime_error(string("[ExecuteSearchThread] error: ") + e.what()));
        }
        catch(...)
        {
            status = boost::copy_exception(runtime_error("[ExecuteSearchThread] unknown error"));
        }
    }

    void ExecuteSearch()
    {
        size_t numProcessors = (size_t) g_numWorkers;
        boost::uint32_t numLibSpectra = (boost::uint32_t) librarySpectra.size();
        // Clear any previous batches
        libBatchValues.clear();

        vector<size_t> current;
        size_t currentBatchSize = 0;
        // For each spectrum
        for( size_t i=0; i < numLibSpectra; ++i ) 
        {
            // Check the result size, if it exceeds the limit, then push back the
            // current list into the vector and get a fresh list
            ++currentBatchSize;
            if(currentBatchSize>g_rtConfig->LibraryBatchSize) 
            {
                libBatchValues.push_back(current);
                current.clear();
                currentBatchSize = 0;
            }
            current.push_back(i);
        }
        // Make sure you push back the last batch
        if(current.size()>0)
            libBatchValues.push_back(current);
        // Check to see if the last batch is not a tiny batch
        if(libBatchValues.back().size()<(g_rtConfig->LibraryBatchSize/5) && libBatchValues.size()>1) 
        {
            vector<size_t> last = libBatchValues.back(); libBatchValues.pop_back();
            vector<size_t> penultimate = libBatchValues.back(); libBatchValues.pop_back();
            penultimate.insert(penultimate.end(),last.begin(),last.end());
            libBatchValues.push_back(penultimate);
        }

        for (size_t i=0; i < libBatchValues.size(); ++i)
            libraryTasks.push(i);

        bpt::ptime start = bpt::microsec_clock::local_time();

        typedef boost::shared_ptr<boost::thread> shared_thread;

        // use list so iterators and references stay valid
        list<pair<shared_thread, ThreadStatus> > workerThreads;

        for (size_t i = 0; i < numProcessors; ++i)
        {
            workerThreads.push_back(make_pair(shared_thread(), ThreadStatus()));
            workerThreads.back().first.reset(new boost::thread(ExecuteSearchThread, boost::ref(workerThreads.back().second)));
        }
            
        bpt::ptime lastUpdate = start;
            
        set<shared_thread> finishedThreads;
        while (finishedThreads.size() < workerThreads.size())
            BOOST_FOREACH_FIELD((shared_thread& t)(ThreadStatus& status), workerThreads)
            {
                if (t->timed_join(bpt::milliseconds(round(g_rtConfig->StatusUpdateFrequency * 1000))))
                    finishedThreads.insert(t);

                if (status.exception)
                {
                    //boost::rethrow_exception(status.exception);
                    cerr << boost::to_string(status.exception) << endl;
                    _exit(1); // HACK: necessary to avoid runtime errors?
                }

                bpt::ptime current = bpt::microsec_clock::local_time();

                // only make one update per StatusUpdateFrequency seconds
                if ((current - lastUpdate).total_microseconds() / 1e6 < g_rtConfig->StatusUpdateFrequency)
                    continue;

                lastUpdate = current;
                bpt::time_duration elapsed = current - start;

                float spectraPerSec = static_cast<float>(searchStatistics.numSpectraSearched) / elapsed.total_microseconds() * 1e6;
                bpt::time_duration estimatedTimeRemaining(0, 0, round((numLibSpectra - searchStatistics.numSpectraSearched) / spectraPerSec));

                cout << "Searched " << searchStatistics.numSpectraSearched << " of " << numLibSpectra << " spectra; "
                    << round(spectraPerSec) << " per second, "
                    << format_date_time("%H:%M:%S", bpt::time_duration(0, 0, elapsed.total_seconds())) << " elapsed, "
                    << format_date_time("%H:%M:%S", estimatedTimeRemaining) << " remaining." << endl;
                PRINT_PROFILERS(cout,"profile:");
            }
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
        using namespace boost::xpressive;
        using namespace boost::xpressive::regex_constants;

        DigestionSites sites_;
        sregex cleavageAgentRegex = sregex::compile(g_rtConfig->cleavageAgentRegex);
        if(!g_rtConfig->cleavageAgentRegex.empty())
        {
            // Get the bounding iterators
            std::string::const_iterator start = sequence.begin();
            std::string::const_iterator end = sequence.end();
            smatch what;
            match_flag_type flags = match_default;
            // Find a possible cleavage site from last match to the end of the sequence
            while (regex_search(start, end, what, cleavageAgentRegex, flags))
            {
                sites_.insert(int(what[0].first-sequence.begin()-1));

                // update search position and flags
                start = what[0].second;
                flags = flags | match_prev_avail |  match_not_bol;
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
            for (int z=0; z < (int) (*sItr)->resultsByCharge.size(); ++z)
                BOOST_FOREACH(const Spectrum::SearchResultPtr& result, (*sItr)->resultsByCharge[z])
                    results[result->sequence()].push_back(&const_cast<SearchResult&>(*result));

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
                        (*rItr)->proteins.insert( proteins[i].getName() );    
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
        for(SpectraList::const_iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr) 
        {
            for(int z = 0; z < (int) (*sItr)->resultsByCharge.size(); ++z)
            {
                vector<Spectrum::SearchResultPtr> resultsToPurge;
                BOOST_FOREACH(const Spectrum::SearchResultPtr& r, (*sItr)->resultsByCharge[z])
                    if(r->proteins.size()==0)
                        resultsToPurge.push_back(r);
                numOrphanResults += resultsToPurge.size();
                BOOST_FOREACH(const Spectrum::SearchResultPtr& r, resultsToPurge)
                    (*sItr)->resultsByCharge[z].erase(r);
            }
        }
        if(numOrphanResults>0) 
        {
            cout << "Purged " << numOrphanResults << " orphan results." << endl;
            cout << "Orphan results have no associated proteins in the given FASTA database" << endl;
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
            estimatedResultsSize += g_rtConfig->MaxResultRank;
            if(estimatedResultsSize>g_rtConfig->ResultsPerBatch) 
            {
                batches.push_back(current);
                current.reset(new SpectraList());
                estimatedResultsSize = g_rtConfig->MaxResultRank * 2;
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
        // Get the command line arguments and process them
        vector< string > args;
        for( int i=0; i < argc; ++i )
            args.push_back( argv[i] );

        if( InitProcess( args ) )
            return 1;

        // Get the database name
        g_dbFilename = g_rtConfig->ProteinDatabase;
        int numSpectra = 0;

        INIT_PROFILERS(13)

        for( size_t i=1; i < args.size(); ++i )
        {
            //cout << g_hostString << " is reading spectra from files matching mask \"" << args[i] << "\"" << endl;
            FindFilesByMask( args[i], g_inputFilenames );
        }

        if( g_inputFilenames.empty() )
        {
            cerr << "No data sources found with the given filemasks." << endl;
            return 1;
        }

        if( !TestFileType( g_dbFilename, "fasta" ) )
            return 1;

        // Read the protein database
        cout << "Reading \"" << g_dbFilename << "\"" << endl;
        Timer readTime(true);
        try
        {
            proteins = proteinStore( g_rtConfig->DecoyPrefix );
            proteins.readFASTA( g_dbFilename );
        }
        catch (std::exception& e)
        {
            cout << "Error loading protein database: " << e.what() << endl;
            return 1;
        }
        cout << "Read " << proteins.size() << " proteins; " << readTime.End() << " seconds elapsed." << endl;

        if (bal::iends_with(g_rtConfig->SpectralLibrary, ".msp") ||
            bal::iends_with(g_rtConfig->SpectralLibrary, ".sptxt"))
        {
            if (!bfs::exists(g_rtConfig->SpectralLibrary + ".index"))
            {
                // index the library if it's not already indexed
                try
                {
                    LibraryBabelFish converter(g_rtConfig->SpectralLibrary);
                    converter.initializeDatabase();
                    converter.indexLibrary();
                }
                catch (std::exception& e)
                {
                    cout << "Error indexing spectral library: " << e.what() << endl;
                    return 1;
                }
            }
            else
                cout << "Using existing index for \"" << g_rtConfig->SpectralLibrary << "\"" << endl;

            g_rtConfig->SpectralLibrary += ".index";
        }

        fileList_t finishedFiles;
        fileList_t::iterator fItr;
        // For each input spectra file
        for( fItr = g_inputFilenames.begin(); fItr != g_inputFilenames.end(); ++fItr )
        {
            // Read the associated spectral library. Repetitive reading is necessary to keep
            // the memory bounds in check.
            try
            {
                librarySpectra.loadLibrary(g_rtConfig->SpectralLibrary);
            }
            catch (std::exception& e)
            {
                cout << "Encountered an error loading spectral library: " << e.what() << endl;
                return 1;
            }

            // randomize order of the spectra to optimize work 
            // distribution in the multi-threading mode.
            //librarySpectra.random_shuffle();

            Timer fileTime(true);

            // Hold the spectra objects
            spectra.clear();
            // Holds the map of parent mass to corresponding spectra
            avgSpectraByChargeState.clear();
            monoSpectraByChargeState.clear();
            searchStatistics = SearchStatistics();

            cout << "Reading spectra from file \"" << *fItr << "\"" << endl;
            finishedFiles.insert( *fItr );

            Timer readTime(true);

            // Read the spectra
            try
            {
                spectra.readPeaks( *fItr, 0, -1, 2, g_rtConfig->SpectrumListFilters );
            }
            catch (std::exception& e )
            {
                cerr << "Error reading experimental spectra: " << e.what() << endl;
                return 1;
            }

            // Compute the peak counts
            int totalPeakCount = 0;
            numSpectra = (int) spectra.size();
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
                totalPeakCount += (*sItr)->peakPreCount;

            cout << "Read " << numSpectra << " experimental spectra with " << totalPeakCount << " peaks; " << readTime.End() << " seconds elapsed." << endl;

            int skip = 0;
            if( numSpectra == 0 )
            {
                cout << "Skipping a file with no experimental spectra." << endl;
                skip = 1;
            }

            Timer searchTime;
            string startTime;
            string startDate;
            vector< size_t > opcs; // original peak count statistics
            vector< size_t > fpcs; // filtered peak count statistics

            // If the file has spectra
            if( !skip )
            {
                // prepare the spectra
                cout << "Preparing " << numSpectra << " experimental spectra." << endl;
                Timer prepareTime(true);
                PrepareSpectra();
                cout << "Finished preparing experimental spectra; " << prepareTime.End() << " seconds elapsed." << endl;
                numSpectra = (int) spectra.size();

                skip = 0;
                if( numSpectra == 0 )
                {
                    cout << "Skipping a file with no suitable experimental spectra." << endl;
                    skip = 1;
                }
                // If the file has spectra to search
                if( !skip )
                {
                    opcs = spectra.getOriginalPeakCountStatistics();
                    fpcs = spectra.getFilteredPeakCountStatistics();
                    cout << "Mean original (filtered) experimental peak count: " << opcs[5] << " (" << fpcs[5] << ")" << endl;
                    cout << "Min/max original (filtered) experimental peak count: " << opcs[0] << " (" << fpcs[0] << ") / " << opcs[1] << " (" << fpcs[1] << ")" << endl;
                    cout << "Original (filtered) experimental peak count at 1st/2nd/3rd quartiles: " <<
                        opcs[2] << " (" << fpcs[2] << "), " <<
                        opcs[3] << " (" << fpcs[3] << "), " <<
                        opcs[4] << " (" << fpcs[4] << ")" << endl;
                    float filter = 1.0f - ( (float) fpcs[5] / (float) opcs[5] );
                    cout << "Filtered out " << filter * 100.0f << "% of experimental peaks." << endl;

                    InitWorkerGlobals();
                    // Start the search
                    if( !g_rtConfig->EstimateSearchTimeOnly )
                    {
                        cout << "Commencing library search on " << numSpectra << " spectra." << endl;
                        startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
                        ExecuteSearch();
                        cout << "Finished library search; " << searchTime.End() << " seconds elapsed." << endl;
                        cout << "Overall stats: " << (string) searchStatistics << endl;
                    } else
                    {
                        cout << "Estimating the count of sequence comparisons to be done." << endl;
                        ExecuteSearch();
                        double estimatedComparisonsPerSpectrum = searchStatistics.numComparisonsDone / (double) searchStatistics.numSpectraQueried;
                        boost::int64_t estimatedTotalComparisons = (boost::int64_t) (estimatedComparisonsPerSpectrum * librarySpectra.size());
                        cout << "Will make an estimated total of " << estimatedTotalComparisons << " spectrum comparisons." << endl;
                        skip = 1;
                    }
                }

                DestroyWorkerGlobals();
                // Write the output
                if( !skip ) {
                    if ( g_rtConfig->FASTARefreshResults )
                    {
                        Timer mapTimer(true);
                        cout << "Mapping library peptide matches to fasta database." << endl;
                        findProteinMatches();
                        purgeOrphanResults();
                        cout << "Finished mapping; " << mapTimer.End() << " seconds elapsed." << endl;
                    }
                    WriteOutputToFile( *fItr, startTime, startDate, searchTime.End(), opcs, fpcs, searchStatistics );
                    cout << "Finished file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
                    //PRINT_PROFILERS(cout,"old");
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

    // HACK: avoid crashing at exit on Windows (probably a conflict between Boost and the .NET vendor DLLs)
    #ifdef WIN32
        TerminateProcess(GetCurrentProcess(), result);
    #else
        return result;
    #endif
}
