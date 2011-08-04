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
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#include "stdafx.h"
#include "myrimatch.h"
#include "boost/lockfree/fifo.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/data/proteome/Version.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "PTMVariantList.h"
#include "myrimatchVersion.hpp"

namespace freicore
{
namespace myrimatch
{
	proteinStore					proteins;
    boost::lockfree::fifo<size_t>   proteinTasks;
    SearchStatistics                searchStatistics;

	SpectraList						spectra;
    SpectraMassMapList				avgSpectraByChargeState;
	SpectraMassMapList				monoSpectraByChargeState;

	RunTimeConfig*					g_rtConfig;

	int InitProcess( argList_t& args )
	{
		//cout << g_hostString << " is initializing." << endl;
		if( g_pid == 0 )
		{
          cout << "MyriMatch " << Version::str() << " (" << Version::LastModified() << ")\n" <<
                  "FreiCore " << freicore::Version::str() << " (" << freicore::Version::LastModified() << ")\n" <<
                  "ProteoWizard MSData " << pwiz::msdata::Version::str() << " (" << pwiz::msdata::Version::LastModified() << ")\n" <<
                  "ProteoWizard Proteome " << pwiz::proteome::Version::str() << " (" << pwiz::proteome::Version::LastModified() << ")\n" <<
					MYRIMATCH_LICENSE << endl;
		}

		g_rtConfig = new RunTimeConfig;
		g_rtSharedConfig = (BaseRunTimeConfig*) g_rtConfig;
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
						cerr << "Unable to find runtime configuration at \"" << args[i+1] << "\"." << endl;
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
				cerr << "Not enough arguments.\nUsage: " << args[0] << " [-ProteinDatabase <FASTA protein database filepath>] <input spectra filemask 1> [input spectra filemask 2] ..." << endl;
				return 1;
			}

			if( !g_rtConfig->initialized() )
			{
				if( g_rtConfig->initializeFromFile() )
				{
					cerr << "Could not find the default configuration file (hard-coded defaults in use)." << endl;
				}
			}

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
			cerr << "No FASTA protein database specified.\nUsage: " << args[0] << " [-ProteinDatabase <FASTA protein database filepath>] <input spectra filemask 1> [input spectra filemask 2] ..." << endl;
			return 1;
		}

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

		if( !g_numChildren )
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

		//cout << g_hostString << " is precaching factorials up to " << (int) maxPeakSpace << "." << endl;
		g_lnFactorialTable.resize( maxPeakBins );
		//cout << g_hostString << " finished precaching factorials." << endl;

		if( !g_numChildren )
		{
			//cout << "Smallest observed precursor is " << g_rtConfig->curMinPeptideMass << " Da." << endl;
			//cout << "Largest observed precursor is " << g_rtConfig->curMaxPeptideMass << " Da." << endl;
            cout << "Min. effective peptide mass is " << g_rtConfig->curMinPeptideMass << endl;
            cout << "Max. effective peptide mass is " << g_rtConfig->curMaxPeptideMass << endl;
            cout << "Min. effective peptide length is " << curMinPeptideLength << endl;
            cout << "Max. effective peptide length is " << curMaxPeptideLength << endl;
		}

		return 0;
	}

	void DestroyWorkerGlobals()
	{
	}

    void ComputeXCorrs()
    {        
        Timer timer;
        timer.Begin();

		if( g_numChildren == 0 )
            cout << "Computing cross-correlations." << endl;

        // For each spectrum, iterate through its result set and compute the XCorr.
        BOOST_FOREACH(Spectrum* s, spectra)
            s->ComputeXCorrs();

        if( g_numChildren == 0 )
            cout << "Finished computing cross-correlations; " << timer.End() << " seconds elapsed." << endl;
    }

	void WriteOutputToFile(	const string& dataFilename,
							string startTime,
							string startDate,
							float totalSearchTime,
							vector< size_t > opcs,
							vector< size_t > fpcs,
							SearchStatistics& overallStats )
	{
		int numSpectra = 0;
		int numMatches = 0;
		int numLoci = 0;

		string filenameAsScanName = basename( MAKE_PATH_FOR_BOOST(dataFilename) );

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
		fileParams["SearchEngine: Name"] = "MyriMatch";
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

		spectra.write(dataFilename,
                      g_rtConfig->outputFormat,
                      g_rtConfig->OutputSuffix,
                      "MyriMatch",
                      Version::str(),
                      "http://forge.fenchurch.mc.vanderbilt.edu/projects/myrimatch/",
                      g_dbPath + g_dbFilename,
                      g_rtConfig->cleavageAgentRegex,
                      g_rtConfig->decoyPrefix,
                      fileParams);
	}

	void PrepareSpectra()
    {
		int numSpectra = (int) spectra.size();

		Timer timer;

		if( g_numChildren == 0 )
		{
			cout << "Trimming spectra with less than " << g_rtConfig->minIntensityClassCount << " peaks." << endl;
		}

		int preTrimCount = spectra.filterByPeakCount ( g_rtConfig->minIntensityClassCount );
		//int preTrimCount = spectra.filterByPeakCount( 10 );
		numSpectra = (int) spectra.size();

		if( g_numChildren == 0 )
		{
			cout << "Trimmed " << preTrimCount << " spectra for being too sparse." << endl;
			cout << "Preprocessing " << numSpectra << " spectra." << endl;
		}

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

		if( g_numChildren == 0 )
		{
			cout << "Finished preprocessing its spectra; " << timer.End() << " seconds elapsed." << endl;
			cout << "Trimming spectra with less than " << g_rtConfig->minIntensityClassCount << " peaks." << endl;
			cout << "Trimming spectra with precursors too small or large: " <<
					g_rtConfig->MinPeptideMass << " - " << g_rtConfig->MaxPeptideMass << endl;
		}

		int postTrimCount = spectra.filterByPeakCount( g_rtConfig->minIntensityClassCount );

		if( g_numChildren == 0 )
		{
			cout << "Trimmed " << postTrimCount << " spectra." << endl;
		}

	}


	boost::int64_t QuerySequence( const DigestedPeptide& candidate, const string& protein, bool isDecoy, bool estimateComparisonsOnly = false )
	{
		boost::int64_t numComparisonsDone = 0;

        string sequence = PEPTIDE_N_TERMINUS_STRING + candidate.sequence() + PEPTIDE_C_TERMINUS_STRING;
        double monoCalculatedMass = candidate.monoisotopicMass();
        double avgCalculatedMass = candidate.molecularWeight();

		for( int z = 0; z < g_rtConfig->maxChargeStateFromSpectra; ++z )
		{
			int fragmentChargeState = min( z, g_rtConfig->maxFragmentChargeState-1 );
			vector< double > sequenceIons;

            // Look up the spectra that have precursor mass hypotheses between mass + massError and mass - massError
            vector<SpectraMassMap::iterator> candidateHypotheses;
            SpectraMassMap::iterator cur, end;

            end = monoSpectraByChargeState[z].upper_bound( monoCalculatedMass + g_rtConfig->monoPrecursorMassTolerance[z] );
			for( cur = monoSpectraByChargeState[z].lower_bound( monoCalculatedMass - g_rtConfig->monoPrecursorMassTolerance[z] ); cur != end; ++cur )
                candidateHypotheses.push_back(cur);

			end = avgSpectraByChargeState[z].upper_bound( avgCalculatedMass + g_rtConfig->avgPrecursorMassTolerance[z] );
			for( cur = avgSpectraByChargeState[z].lower_bound( avgCalculatedMass - g_rtConfig->avgPrecursorMassTolerance[z] ); cur != end; ++cur )
                candidateHypotheses.push_back(cur);

            BOOST_FOREACH(SpectraMassMap::iterator spectrumHypothesisPair, candidateHypotheses)
			{
                Spectrum* spectrum = spectrumHypothesisPair->second.first;
                PrecursorMassHypothesis& p = spectrumHypothesisPair->second.second;

                boost::shared_ptr<SearchResult> resultPtr(new SearchResult(candidate));
                SearchResult& result = *resultPtr;

				if( !estimateComparisonsOnly )
				{
					START_PROFILER(2);
					if( sequenceIons.empty() )
                    {
						CalculateSequenceIons( candidate,
                                               fragmentChargeState+1,
                                               &sequenceIons,
                                               spectrum->fragmentTypes,
                                               g_rtConfig->UseSmartPlusThreeModel,
                                               0,
                                               0 );
                    }
					STOP_PROFILER(2);
					START_PROFILER(3);
					spectrum->ScoreSequenceVsSpectrum( result, sequence, sequenceIons );
					STOP_PROFILER(3);

					if( result.mvh >= g_rtConfig->MinResultScore )
					{
						START_PROFILER(5);
                        result.proteins.insert(protein);
                        result._isDecoy = isDecoy;
						STOP_PROFILER(5);
					}
				}

				++ numComparisonsDone;

				if( estimateComparisonsOnly )
					continue;

				START_PROFILER(4);
                {
                    boost::mutex::scoped_lock guard(spectrum->mutex);

                    if( isDecoy )
				        ++ spectrum->numDecoyComparisons;
                    else
                        ++ spectrum->numTargetComparisons;

				    if( result.mvh >= g_rtConfig->MinResultScore )
				    {
                        result.precursorMassHypothesis = p;
					    //result.massError = p.massType == MassType_Monoisotopic ? monoCalculatedMass - p.mass
                        //                                                       : avgCalculatedMass - p.mass;

					    // Accumulate score distributions for the spectrum
					    ++ spectrum->mvhScoreDistribution[ (int) (result.mvh+0.5) ];
					    ++ spectrum->mzFidelityDistribution[ (int) (result.mzFidelity+0.5)];

					    spectrum->resultsByCharge[z].add( resultPtr );         
				    }
                }
				STOP_PROFILER(4);
			}
		}

		return numComparisonsDone;
	}

	int ExecuteSearchThread()
	{
        try
        {
            size_t proteinTask;
		    while( true )
		    {
                if (!proteinTasks.dequeue(&proteinTask))
				    break;

			    ++ searchStatistics.numProteinsDigested;

                proteinData p = proteins[proteinTask];

                if (!g_rtConfig->ProteinListFilters.empty() &&
                    g_rtConfig->ProteinListFilters.find(p.getName()) == string::npos)
                {
                    continue;
                }

                Peptide protein(p.getSequence());
                bool isDecoy = p.isDecoy();

                Digestion digestion( protein, g_rtConfig->cleavageAgentRegex, g_rtConfig->digestionConfig );
                for( Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); )
                {
                    ++searchStatistics.numPeptidesGenerated;

                    if (itr->sequence().find_first_of("BXZ") != string::npos)
                    {
                        ++itr;
                        continue;
                    }

                    // a selenopeptide's molecular weight can be lower than its monoisotopic mass!
                    double minMass = min(itr->monoisotopicMass(), itr->molecularWeight());
                    double maxMass = max(itr->monoisotopicMass(), itr->molecularWeight());

                    if( minMass > g_rtConfig->curMaxPeptideMass ||
                        maxMass < g_rtConfig->curMinPeptideMass )
                    {
                        ++itr;
                        continue;
                    }

					PTMVariantList variantIterator( (*itr), g_rtConfig->MaxDynamicMods, g_rtConfig->dynamicMods, g_rtConfig->staticMods, g_rtConfig->MaxPeptideVariants);
                    if(variantIterator.isSkipped)
                    {
                        ++ searchStatistics.numPeptidesSkipped;
                        ++ itr;
                        continue;
                    }

                    searchStatistics.numVariantsGenerated += variantIterator.numVariants;

                    // query each variant
                    do
                    {
                        boost::int64_t queryComparisonCount = QuerySequence( variantIterator.ptmVariant, p.getName(), isDecoy, g_rtConfig->EstimateSearchTimeOnly );
                        if( queryComparisonCount > 0 )
                            searchStatistics.numComparisonsDone += queryComparisonCount;
                    }
                    while (variantIterator.next());

                    ++itr;
                }
            }
        } catch( std::exception& e )
        {
            cerr << " terminated with an error: " << e.what() << endl;
        } catch(...)
        {
            cerr << " terminated with an unknown error." << endl;
        }

		return 0;
	}

	void ExecuteSearch()
	{
		size_t numProcessors = (size_t) g_numWorkers;
        boost::uint32_t numProteins = (boost::uint32_t) proteins.size();

		for (size_t i=0; i < numProteins; ++i)
			proteinTasks.enqueue(i);

        bpt::ptime start = bpt::microsec_clock::local_time();

        boost::thread_group workerThreadGroup;
        vector<boost::thread*> workerThreads;

		for (size_t i = 0; i < numProcessors; ++i)
            workerThreads.push_back(workerThreadGroup.create_thread(&ExecuteSearchThread));

        if (g_numChildren > 0)
        {
            // MPI jobs do a simple join_all
            workerThreadGroup.join_all();

            // xcorrs are calculated just before sending back results
        }
        else
        {
            bpt::ptime lastUpdate = start;

            for (size_t i=0; i < numProcessors; ++i)
            {
                // returns true if the thread finished before the timeout;
                // (each thread index is joined until it finishes)
                if (!workerThreads[i]->timed_join(bpt::seconds(round(g_rtConfig->StatusUpdateFrequency))))
                    --i;

                bpt::ptime current = bpt::microsec_clock::local_time();

                // only make one update per StatusUpdateFrequency seconds
                if ((current - lastUpdate).total_microseconds() / 1e6 < g_rtConfig->StatusUpdateFrequency)
                    continue;

                lastUpdate = current;
                bpt::time_duration elapsed = current - start;

			    float proteinsPerSec = static_cast<float>(searchStatistics.numProteinsDigested) / elapsed.total_microseconds() * 1e6;
                bpt::time_duration estimatedTimeRemaining(0, 0, round((numProteins - searchStatistics.numProteinsDigested) / proteinsPerSec));

		        cout << "Searched " << searchStatistics.numProteinsDigested << " of " << numProteins << " proteins; ";
                //cout << searchStatistics.numPeptidesGenerated << " peptides; "
                //     << searchStatistics.numVariantsGenerated << " variants; ";
                //if (searchStatistics.numPeptidesSkipped > 0)
                //    cout << searchStatistics.numPeptidesSkipped << " skipped; ";
                //cout << searchStatistics.numComparisonsDone << " comparisons; ";
                cout << round(proteinsPerSec) << " per second, "
                     << format_date_time("%H:%M:%S", bpt::time_duration(0, 0, elapsed.total_seconds())) << " elapsed, "
                     << format_date_time("%H:%M:%S", estimatedTimeRemaining) << " remaining." << endl;

		        //float candidatesPerSec = threadInfo->stats.numComparisonsDone / totalSearchTime;
		        //float estimatedTimeRemaining = float( numCandidates - threadInfo->stats.numComparisonsDone ) / candidatesPerSec / numThreads;
		        //cout << threadInfo->workerHostString << " has made " << threadInfo->stats.numComparisonsDone << " of about " << numCandidates << " comparisons; " <<
		        //		candidatesPerSec << " per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
		    }

            // compute xcorr for top ranked results
            if( g_rtConfig->ComputeXCorr )
                ComputeXCorrs();
        }
	}

    // Shared pointer to SpectraList.
    typedef boost::shared_ptr<SpectraList> SpectraListPtr;
    /**
        This function takes a spectra list and splits them into small batches as dictated by
        ResultsPerBatch variable. This function also checks to make sure that the last batch
        is not smaller than 1000 spectra.
    */
    vector<SpectraListPtr> estimateSpectralBatches()
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
        This function is the entry point into the MyriMatch search engine. This
        function process the command line arguments, sets up the search, triggers
        the threads that perform the search, and writes out the result file.
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

		INIT_PROFILERS(14)

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
				cout << "No data sources found with the given filemasks." << endl;
				return 1;
			}

			if( !TestFileType( g_dbFilename, "fasta" ) )
				return 1;

            // Read the protein database
			cout << "Reading \"" << g_dbFilename << "\"" << endl;
			Timer readTime(true);
			try
			{
                proteins = proteinStore( g_rtConfig->decoyPrefix );
                proteins.readFASTA( g_dbFilename, " ", g_rtConfig->automaticDecoys );
			} catch( std::exception& e )
			{
				cout << g_hostString << " had an error: " << e.what() << endl;
				return 1;
			}
			cout << "Read " << proteins.size() << " proteins; " << readTime.End() << " seconds elapsed." << endl;

            // randomize order of the proteins to optimize work distribution
            // in the MPI and multi-threading mode.
			proteins.random_shuffle();

            // If we are running in clster mode and this is a master process then
            // compute the protein batch size using numer of child processes. Each 
            // child process is sent all spectra to be searched against a batch of
            // protein sequences.
			#ifdef USE_MPI
				if( g_numChildren > 0 )
				{
					g_rtConfig->ProteinBatchSize = (int) ceil( (float) proteins.size() / (float) g_numChildren / (float) g_rtConfig->NumBatches );
					cout << "Dynamic protein batch size is " << g_rtConfig->ProteinBatchSize << endl;
				}
			#endif

			fileList_t finishedFiles;
			fileList_t::iterator fItr;
            // For each input spectra file
			for( fItr = g_inputFilenames.begin(); fItr != g_inputFilenames.end(); ++fItr )
			{
				Timer fileTime(true);

				spectra.clear();
				avgSpectraByChargeState.clear();
                monoSpectraByChargeState.clear();
                searchStatistics = SearchStatistics();

				cout << "Reading spectra from file \"" << *fItr << "\"" << endl;
				finishedFiles.insert( *fItr );

				Timer readTime(true);

                // Read the spectra
				try
				{
					spectra.readPeaks( *fItr,
                                       0, -1,
                                       2, // minMsLevel
                                       g_rtConfig->SpectrumListFilters,
                                       g_rtConfig->NumChargeStates);
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

				cout << "Read " << numSpectra << " spectra with " << totalPeakCount << " peaks; " << readTime.End() << " seconds elapsed." << endl;

				int skip = 0;
				if( numSpectra == 0 )
				{
					cout << "Skipping a file with no spectra." << endl;
					skip = 1;
				}

                // If the file has no spectra, then tell the child processes to skip
				#ifdef USE_MPI
					if( g_numChildren > 0 && !g_rtConfig->EstimateSearchTimeOnly )
					{
						g_rtConfig->SpectraBatchSize = (int) ceil( (float) numSpectra / (float) g_numChildren / (float) g_rtConfig->NumBatches );
						cout << "Dynamic spectra batch size is " << g_rtConfig->SpectraBatchSize << endl;
					}

					for( int p=0; p < g_numChildren; ++p )
						MPI_Ssend( &skip,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );
				#endif

				Timer searchTime;
				string startTime;
				string startDate;
				vector< size_t > opcs; // original peak count statistics
				vector< size_t > fpcs; // filtered peak count statistics

                // If the file has spectra
				if( !skip )
				{
                    // If this is a master process and we are in MPI mode.
					if( g_numProcesses > 1 && !g_rtConfig->EstimateSearchTimeOnly )
					{
						#ifdef USE_MPI
                        // Send some spectra away to the child nodes for processing
						cout << "Sending spectra to worker nodes to prepare them for search." << endl;
						Timer prepareTime(true);
						TransmitUnpreparedSpectraToChildProcesses();
						spectra.clear();
						ReceivePreparedSpectraFromChildProcesses();
						numSpectra = (int) spectra.size();
                        
						skip = 0;
						if( numSpectra == 0 )
						{
							cout << "Skipping a file with no suitable spectra." << endl;
							skip = 1;
						}
                        
                        // If all processed spectra gets dropped out then
                        // there is no need to proceed.
						for( int p=0; p < g_numChildren; ++p )
							MPI_Ssend( &skip,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );

						if( !skip )
						{
                            // Get peak count stats
							opcs = spectra.getOriginalPeakCountStatistics();
							fpcs = spectra.getFilteredPeakCountStatistics();
							cout << "Mean original (filtered) peak count: " << opcs[5] << " (" << fpcs[5] << ")" << endl;
							cout << "Min/max original (filtered) peak count: " << opcs[0] << " (" << fpcs[0] << ") / " << opcs[1] << " (" << fpcs[1] << ")" << endl;
							cout << "Original (filtered) peak count at 1st/2nd/3rd quartiles: " <<
									opcs[2] << " (" << fpcs[2] << "), " <<
									opcs[3] << " (" << fpcs[3] << "), " <<
									opcs[4] << " (" << fpcs[4] << ")" << endl;

							float filter = 1.0f - ( (float) fpcs[5] / (float) opcs[5] );
							cout << "Filtered out " << filter * 100.0f << "% of peaks." << endl;
							cout << "Prepared " << numSpectra << " spectra; " << prepareTime.End() << " seconds elapsed." << endl;
                            
                            // Init the globals
							InitWorkerGlobals();
                            
                            // List to store finished spectra
							SpectraList finishedSpectra;
                            // Split the spectra into batches if needed
                            vector<SpectraListPtr> batches = estimateSpectralBatches();
                            if(batches.size()>1)
                                cout << "Splitting spectra into " << batches.size() << " batches for search." << endl;
                            startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
                            // For each spectral batch
                            size_t batchIndex = 0;
                            for(vector<SpectraListPtr>::iterator bItr = batches.begin(); bItr != batches.end(); ++bItr) 
							{
                                // Variables to report batch progess to the user
                                ++batchIndex;
                                stringstream batchString;
                                batchString << "";
                                if(batches.size()>1)
                                    batchString << " (" << batchIndex << " of " << batches.size() << " batches)";
                                // Clear the master list and populate it with a small batch
								spectra.clear( false );
                                spectra.insert((*bItr)->begin(), (*bItr)->end(), spectra.end());
                                // Check to see if we are processing the last batch.
                                int lastBatch = 0;
                                if((*bItr) == batches.back())
                                    lastBatch = 1;
                                // Transmit spectra to all children. Also tell them if this is
                                // the last batch of the spectra they would be getting from the parent.
                                cout << "Sending some prepared spectra to all worker nodes from a pool of " << spectra.size() << " spectra" << batchString.str() << "." << endl;
                                try
                                {
								    Timer sendTime(true);
								    numSpectra = TransmitSpectraToChildProcesses(lastBatch);
								    cout << "Finished sending " << numSpectra << " prepared spectra to all worker nodes; " <<
										    sendTime.End() << " seconds elapsed." << endl;
                                } catch( std::exception& e )
                                {
                                    cout << g_hostString << " had an error transmitting prepared spectra: " << e.what() << endl;
                                    MPI_Abort( MPI_COMM_WORLD, 1 );
                                }
                                // Transmit the proteins and start the search.
								cout << "Commencing database search on " << numSpectra << " spectra" << batchString.str() << "." << endl;
                                try
                                {
								    Timer batchTimer(true); batchTimer.Begin();
								    TransmitProteinsToChildProcesses();
								    cout << "Finished database search; " << batchTimer.End() << " seconds elapsed" << batchString.str() << "." << endl;
                                } catch( std::exception& e )
                                {
                                    cout << g_hostString << " had an error transmitting protein batches: " << e.what() << endl;
                                    MPI_Abort( MPI_COMM_WORLD, 1 );
                                }
                                // Get the results
								cout << "Receiving search results for " << numSpectra << " spectra" << batchString.str() << "." << endl;
                                try
                                {
								    Timer receiveTime(true);
								    ReceiveResultsFromChildProcesses(((*bItr) == batches.front()));
								    cout << "Finished receiving search results; " << receiveTime.End() << " seconds elapsed." << endl;
                                } catch( std::exception& e )
                                {
                                    cout << g_hostString << " had an error receiving results: " << e.what() << endl;
                                    MPI_Abort( MPI_COMM_WORLD, 1 );
                                }
								cout << "Overall stats: " << (string) searchStatistics << endl;
                                
                                // Store the searched spectra in a list and clear the 
                                // master list for next batch
								finishedSpectra.insert( spectra.begin(), spectra.end(), finishedSpectra.end() );
								spectra.clear( false );
                                (*bItr)->clear( false );
							}
                            searchTime.End();

                            // Move the searched spectra from temporary list to the master list
                            spectra.clear( false );
                            spectra.insert(finishedSpectra.begin(), finishedSpectra.end(), spectra.end() );
							finishedSpectra.clear(false);

                            // Spectra are randomly shuffled for load distribution during batching process
                            // Sort them back by ID.
                            spectra.sort( spectraSortByID() );

							DestroyWorkerGlobals();
						}

						#endif
					} else
					{
                        // If we are not in the MPI mode then prepare the spectra
                        // ourselves
						cout << "Preparing " << numSpectra << " spectra." << endl;
						Timer prepareTime(true);
						PrepareSpectra();
						cout << "Finished preparing spectra; " << prepareTime.End() << " seconds elapsed." << endl;
						numSpectra = (int) spectra.size();

						skip = 0;
						if( numSpectra == 0 )
						{
							cout << "Skipping a file with no suitable spectra." << endl;
							skip = 1;
						}
                        // If the file has spectra to search
						if( !skip )
						{
							opcs = spectra.getOriginalPeakCountStatistics();
							fpcs = spectra.getFilteredPeakCountStatistics();
							cout << "Mean original (filtered) peak count: " << opcs[5] << " (" << fpcs[5] << ")" << endl;
							cout << "Min/max original (filtered) peak count: " << opcs[0] << " (" << fpcs[0] << ") / " << opcs[1] << " (" << fpcs[1] << ")" << endl;
							cout << "Original (filtered) peak count at 1st/2nd/3rd quartiles: " <<
									opcs[2] << " (" << fpcs[2] << "), " <<
									opcs[3] << " (" << fpcs[3] << "), " <<
									opcs[4] << " (" << fpcs[4] << ")" << endl;
							float filter = 1.0f - ( (float) fpcs[5] / (float) opcs[5] );
							cout << "Filtered out " << filter * 100.0f << "% of peaks." << endl;

							InitWorkerGlobals();
                            // Start the search
							if( !g_rtConfig->EstimateSearchTimeOnly )
							{
								cout << "Commencing database search on " << numSpectra << " spectra." << endl;
								startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
								ExecuteSearch();
								cout << "Finished database search; " << searchTime.End() << " seconds elapsed." << endl;
				
								cout << "Overall stats: " << (string) searchStatistics << endl;
							} else
                            {
                                cout << "Estimating the count of sequence comparisons to be done." << endl;
                                ExecuteSearch();
                                double estimatedComparisonsPerProtein = searchStatistics.numComparisonsDone / (double) searchStatistics.numProteinsDigested;
                                boost::int64_t estimatedTotalComparisons = (boost::int64_t) (estimatedComparisonsPerProtein * proteins.size());
                                cout << "Will make an estimated total of " << estimatedTotalComparisons << " sequence comparisons." << endl;
                                //cout << g_hostString << " will make an estimated " << estimatedComparisonsPerProtein << " sequence comparisons per protein." << endl;
								skip = 1;
                            }
						}

						DestroyWorkerGlobals();
					}
                    // Write the output
                    if( !skip )
                    {
                        WriteOutputToFile( *fItr, startTime, startDate, searchTime.End(), opcs, fpcs, searchStatistics );
                        cout << "Finished file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
                        //PRINT_PROFILERS(cout,"old");
                    }

				}
                
                // Tell the child nodes that we are all done if there are 
                // no other spectral data files to process
				#ifdef USE_MPI
				int done = ( ( g_inputFilenames.size() - finishedFiles.size() ) == 0 ? 1 : 0 );
				for( int p=0; p < g_numChildren; ++p )
					MPI_Ssend( &done,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );
				#endif
			}
		}
		#ifdef USE_MPI
		else
		{
            if( g_rtConfig->EstimateSearchTimeOnly )
                return 0; // nothing to do

			int allDone = 0;

			while( !allDone )
			{
				int skip;
				MPI_Recv( &skip,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );

				if( !skip )
				{
					SpectraList preparedSpectra;

					while( ReceiveUnpreparedSpectraBatchFromRootProcess() )
					{
						PrepareSpectra();
						preparedSpectra.insert( spectra.begin(), spectra.end(), preparedSpectra.end() );
						spectra.clear( false );
					}

					//for( int i=0; i < (int) preparedSpectra.size(); ++i )
					//	cout << preparedSpectra[i]->id.nativeID << " " << preparedSpectra[i]->peakData.size() << endl;

					TransmitPreparedSpectraToRootProcess( preparedSpectra );

					preparedSpectra.clear();
                    

					MPI_Recv( &skip,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );

					if( !skip )
					{

						int done = 0;
						do
						{
                            try
                            {
							    done = ReceiveSpectraFromRootProcess();
                            } catch( std::exception& e )
                            {
                                cout << g_hostString << " had an error receiving prepared spectra: " << e.what() << endl;
                                MPI_Abort( MPI_COMM_WORLD, 1 );
                            }

							InitWorkerGlobals();

							int numBatches = 0;
                            try
                            {
							    while( ReceiveProteinBatchFromRootProcess() )
							    {
								    ++ numBatches;

								    ExecuteSearch();
							    }
                            } catch( std::exception& e )
                            {
                                cout << g_hostString << " had an error receiving protein batch: " << e.what() << endl;
                                MPI_Abort( MPI_COMM_WORLD, 1 );
                            }

							cout << g_hostString << " stats: " << numBatches << " batches; " << (string) searchStatistics << endl;

                            try
                            {
                                // compute xcorr for top ranked results
                                if( g_rtConfig->ComputeXCorr )
                                    ComputeXCorrs();

							    TransmitResultsToRootProcess();
                            } catch( std::exception& e )
                            {
                                cout << g_hostString << " had an error transmitting results: " << e.what() << endl;
                                MPI_Abort( MPI_COMM_WORLD, 1 );
                            }

							DestroyWorkerGlobals();
							spectra.clear();
                            avgSpectraByChargeState.clear();
							monoSpectraByChargeState.clear();
						} while( !done );
					}
				}
				MPI_Recv( &allDone,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );
			} // end of while
		} // end of if
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
		result = myrimatch::ProcessHandler( argc, argv );
	} catch( std::exception& e )
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
