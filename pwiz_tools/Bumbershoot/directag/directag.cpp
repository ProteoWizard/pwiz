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
// The Original Code is the DirecTag peptide sequence tagger.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

#include "stdafx.h"
#include "directag.h"
#include "Histogram.h"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/data/proteome/Version.hpp"
#include "svnrev.hpp"

#include "ranker.h"
#include "writeHighQualSpectra.h"

using namespace freicore;

namespace freicore
{
namespace directag
{
    SpectraList                  spectra;
	map< char, float >           compositionInfo;

	RunTimeConfig*               g_rtConfig;

	simplethread_mutex_t         resourceMutex;

	// Code for ScanRanker
	vector<int>				mergedSpectraIndices;
	vector<int>				highQualSpectraIndices;

    int Version::Major()                {return 1;}
    int Version::Minor()                {return 3;}
    int Version::Revision()             {return SVN_REV;}
    string Version::LastModified()      {return SVN_REVDATE;}
    string Version::str()               
    {
    	std::ostringstream v;
    	v << Major() << "." << Minor() << "." << Revision();
    	return v.str();
    }

	double lnCombin( int a, int b ) { return lnCombin( a, b, g_lnFactorialTable ); }
	float GetMassOfResidues( const string& a, bool b ) { return g_residueMap->GetMassOfResidues( a, b ); }

	void WriteTagsToTagsFile(	const string& inputFilename,
								string startTime,
								string startDate,
								float totalTaggingTime )
	{
		cout << g_hostString << " is generating output of tags." << endl;

		string filenameAsScanName;
		filenameAsScanName =	inputFilename.substr( inputFilename.find_last_of( SYS_PATH_SEPARATOR )+1,
								inputFilename.find_last_of( '.' ) - inputFilename.find_last_of( SYS_PATH_SEPARATOR )-1 );
		string outputFilename = filenameAsScanName + g_rtConfig->OutputSuffix + ".tags";

		stringstream header;
		header << "H\tTagsGenerator\tDirecTag\n" <<
                  "H\tTagsGeneratorVersion\t" << Version::str() << " (" << Version::LastModified() << ")\n";

		string license( DIRECTAG_LICENSE );
		boost::char_separator<char> delim("\n");
		stokenizer parser( license.begin(), license.begin() + license.length(), delim );
		for( stokenizer::iterator token = parser.begin(); token != parser.end(); ++token )
			header <<"H\t" << *token << "\n";

		header <<	"H\tTagging started at " << startTime << " on " << startDate << ".\n" <<
					"H\tTagging finished at " << GetTimeString() << " on " << GetDateString() << ".\n" <<
					"H\tTotal tagging time: " << totalTaggingTime << " seconds.\n" <<
					"H\tUsed " << g_numProcesses << " processing " << ( g_numProcesses > 1 ? "nodes" : "node" ) << ".\n";/* <<
					"H\tMean original (filtered) peak count: " << opcs[5] << " (" << fpcs[5] << ")\n" <<
					"H\tMin/max original (filtered) peak count: " << opcs[0] << " (" << fpcs[0] << ") / " << opcs[1] << " (" << fpcs[1] << ")\n" <<
					"H\tOriginal (filtered) peak count at 1st/2nd/3rd quartiles: " <<	opcs[2] << " (" << fpcs[2] << "), " <<
																						opcs[3] << " (" << fpcs[3] << "), " <<
																						opcs[4] << " (" << fpcs[4] << ")\n";*/
		if( !g_rtConfig->InlineValidationFile.empty() )
		{
			ofstream classCountsFile( string( filenameAsScanName + g_rtConfig->OutputSuffix + "-class-counts.txt" ).c_str() );
			classCountsFile << "<SpectrumId>\t<MatchedIonIntensityRankHistogram>\n";

			//ofstream scoreHistogramsFile( string( filenameAsScanName + g_rtConfig->OutputSuffix + "-histograms.txt" ).c_str() );
			Histogram<int> totalMatchedIonRanks;
			Histogram<int> totalMatchedBIonRanks;
			Histogram<int> totalMatchedYIonRanks;
			Histogram<int> totalMatchedYWaterLossRanks;
			Histogram<int> totalMatchedBWaterLossRanks;
			Histogram<int> totalMatchedYAmmoniaLossRanks;
			Histogram<int> totalMatchedBAmmoniaLossRanks;
			Histogram<int> totalLongestPathRanks;
			Histogram<int> totalValidLongestPathRanks;
			Histogram<int> totalIntensityRanksums;
			Histogram<int> totalValidIntensityRanksums;
			//Spectrum* s;
			map< float, int > scoreToRanksumMap;

			/*for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
			{
				s = *sItr;

				for( TagList::iterator tItr = s->tagList.begin(); tItr != s->tagList.end(); ++tItr )
				{
					++ totalIntensityRanksums[tItr->ranksum];// += itr->second;
					if( tItr->valid )
						++ totalValidIntensityRanksums[tItr->ranksum];// += itr->second;
				}

				if( !s->resultSet.empty() )
				{
					Histogram<int> matchedIonRanks;
					SearchResultSet::reverse_iterator itr = s->resultSet.rbegin();
					vector< float > ionMasses;
					vector< string > ionLabels;
					bool allIonTypes[4] = { true, true, true, true };
					CalculateSequenceIons( itr->sequence, s->id.charge, &ionMasses, g_rtConfig->UseSmartPlusThreeModel, &ionLabels, 0, allIonTypes, &g_rtConfig->inlineValidationResidues );

					for( size_t i=0; i < ionMasses.size(); ++i )
					{
						PeakData::iterator pItr = s->peakData.findNear( ionMasses[i], g_rtConfig->FragmentMzTolerance );
						if( pItr != s->peakData.end() )
						{
							++ matchedIonRanks[ pItr->second.intensityRank ];
							++ totalMatchedIonRanks[ pItr->second.intensityRank ];
							if( ionLabels[i][0] == 'y' )
							{
								if( ionLabels[i].find( "H2O" ) != string::npos )
									++totalMatchedYWaterLossRanks[ pItr->second.intensityRank ];
								else if( ionLabels[i].find( "NH3" ) != string::npos )
									++totalMatchedYAmmoniaLossRanks[ pItr->second.intensityRank ];
								else
									++totalMatchedYIonRanks[ pItr->second.intensityRank ];
							} else if( ionLabels[i][0] == 'b' )
							{
								if( ionLabels[i].find( "H2O" ) != string::npos )
									++totalMatchedBWaterLossRanks[ pItr->second.intensityRank ];
								else if( ionLabels[i].find( "NH3" ) != string::npos )
									++totalMatchedBAmmoniaLossRanks[ pItr->second.intensityRank ];
								else
									++totalMatchedBIonRanks[ pItr->second.intensityRank ];
							}
						}
					}
				}
			}*/
			classCountsFile << "total\t" << totalMatchedIonRanks << endl;
			classCountsFile << "Ys\t" << totalMatchedYIonRanks << endl;
			classCountsFile << "Bs\t" << totalMatchedBIonRanks << endl;
			classCountsFile << "Y-H2Os\t" << totalMatchedYWaterLossRanks << endl;
			classCountsFile << "B-H2Os\t" << totalMatchedBWaterLossRanks << endl;
			classCountsFile << "Y-NH3s\t" << totalMatchedYAmmoniaLossRanks << endl;
			classCountsFile << "B-NH3s\t" << totalMatchedBAmmoniaLossRanks << endl;
			classCountsFile << "AllIntensityRanksums" << totalIntensityRanksums << endl;
			classCountsFile << "ValidIntensityRanksums" << totalValidIntensityRanksums << endl;
			classCountsFile << "AllLongestPathRanks" << totalLongestPathRanks << endl;
			classCountsFile << "ValidLongestPathRanks" << totalValidLongestPathRanks << endl;
		}

		cout << g_hostString << " is writing tags to \"" << outputFilename << "\"." << endl;
		spectra.writeTags( inputFilename, g_rtConfig->OutputSuffix, header.str(), g_rtConfig->getVariables() );
		spectra.clear();
	}

	// Code for writing ScanRanker metrics file
	void WriteSpecQualMetrics( const string& inputFilename, SpectraList& instance, const string& outFilename)
	{
		cout << g_hostString << " is generating output of quality metrics." << endl;
		string filenameAsScanName;
		filenameAsScanName =	inputFilename.substr( inputFilename.find_last_of( SYS_PATH_SEPARATOR )+1,
								inputFilename.find_last_of( '.' ) - inputFilename.find_last_of( SYS_PATH_SEPARATOR )-1 );

		string outputFilename = (outFilename.empty()) ? (filenameAsScanName + "-ScanRankerMetrics" + ".txt") :  outFilename;
		
		ofstream fileStream( outputFilename.c_str() );

		fileStream << "NativeID\tIndex\tCharge\tBestTagScore\tBestTagTIC\tTagMzRange\tBestTagScoreNorm\tBestTagTICNorm\tTagMzRangeNorm\tScanRankerScore\n" ;
		Spectrum* s;
		for( SpectraList::iterator sItr = instance.begin(); sItr != instance.end(); ++sItr )
		{
			s = *sItr;
			fileStream	<< s->nativeID << '\t'
						<< s->id.index << '\t'
						<< s->id.charge << '\t'
						<< s->bestTagScore << '\t'
						<< s->bestTagTIC << '\t'
						<< s->tagMzRange << '\t'
						<< s->bestTagScoreNorm << '\t'
						<< s->bestTagTICNorm << '\t'
						<< s->tagMzRangeNorm << '\t'
						<< s->qualScore << '\n';
		}
	}

	// Code for calculating ScanRanker score
	void CalculateQualScore( SpectraList& instance)
	{
		vector<float> bestTagScoreList;
		vector<float> bestTagTICList;
		vector<float> tagMzRangeList;
		vector<float> rankedBestTagScoreList;
		vector<float> rankedBestTagTICList;
		vector<float> rankedTagMzRangeList;
		Spectrum* s;
		string rankMethod = "average"; //Can also be "min" or "max" or "default"

		for( SpectraList::iterator sItr = instance.begin(); sItr != instance.end(); ++sItr )
		{
			s = *sItr;
			bestTagScoreList.push_back( s->bestTagScore );
			bestTagTICList.push_back( s->bestTagTIC );
			tagMzRangeList.push_back( s->tagMzRange );
		}

		rankhigh( bestTagScoreList, rankedBestTagScoreList, rankMethod );
		rank( bestTagTICList, rankedBestTagTICList, rankMethod );
		rank( tagMzRangeList, rankedTagMzRangeList, rankMethod );

		int i = 0;
		size_t numTotalSpectra = instance.size();
		for( SpectraList::iterator sItr = instance.begin(); sItr != instance.end(); ++sItr )
		{
			s = *sItr;
			s->bestTagScoreNorm = (rankedBestTagScoreList[i]-1) / (float) numTotalSpectra;
			s->bestTagTICNorm = (rankedBestTagTICList[i]-1) / (float) numTotalSpectra;
			s->tagMzRangeNorm = (rankedTagMzRangeList[i]-1) / (float) numTotalSpectra;
//          s->qualScore = ( rankedBestTagScoreList[i] + rankedBestTagTICList[i] + rankedTagMzRangeList[i] ) / (3 * (float) numTotalSpectra);
			s->qualScore = (s->bestTagScoreNorm + s->bestTagTICNorm + s->tagMzRangeNorm ) / 3;
			++i;
		}
	}

	void PrepareSpectra()
	{
		Timer timer;

		if( g_numChildren == 0 )
		{
			cout << g_hostString << " is trimming spectra with less than " << 10 << " peaks." << endl;
		}

		int preTrimCount = 0;
		try
		{
			preTrimCount = spectra.filterByPeakCount( 10 );
		} catch( exception& e )
		{
			throw runtime_error( string( "trimming spectra: " ) + e.what() );
		} catch( ... )
		{
			throw runtime_error( "trimming spectra" );
		}

		if( g_numChildren == 0 )
		{
			cout << g_hostString << " trimmed " << preTrimCount << " spectra for being too small before peak filtering." << endl;
			cout << g_hostString << " is determining spectrum charge states from " << spectra.size() << " spectra." << endl;
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
			spectra.insert( duplicates.begin(), duplicates.end(), spectra.end() );
			duplicates.clear(false);
		} catch( exception& e )
		{
			throw runtime_error( string( "adding duplicated spectra: " ) + e.what() );
		} catch( ... )
		{
			throw runtime_error( "adding duplicated spectra" );
		}

		if( g_numChildren == 0 )
		{
			cout << g_hostString << " finished determining spectrum charge states; " << timer.End() << " seconds elapsed." << endl;
			cout << g_hostString << " is filtering peaks in " << spectra.size() << " spectra." << endl;
		}

		timer.Begin();
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
		{
			try
			{
				(*sItr)->FilterPeaks();

				//if( !g_rtConfig->MakeSpectrumGraphs )
				//	(*sItr)->peakPreData.clear();
			} catch( exception& e )
			{
				throw runtime_error( string( "filtering peaks in scan: " ) + string( (*sItr)->id ) + e.what() );
			} catch( ... )
			{
				throw runtime_error( "filtering peaks in scan" );
			}
		}

		if( g_numChildren == 0 )
			cout << g_hostString << " finished filtering peaks; " << timer.End() << " seconds elapsed." << endl;

		int postTrimCount = 0;
		postTrimCount = spectra.filterByPeakCount( g_rtConfig->minIntensityClassCount );

		if( g_numChildren == 0 )
			cout << g_hostString << " trimmed " << postTrimCount << " spectra for being too small after peak filtering." << endl;
	}

	vector< int > workerNumbers;
	int numSearched;

	simplethread_return_t ExecutePipelineThread( simplethread_arg_t threadArg )
	{
		simplethread_lock_mutex( &resourceMutex );
		simplethread_id_t threadId = simplethread_get_id();
		WorkerThreadMap* threadMap = (WorkerThreadMap*) threadArg;
		WorkerInfo* threadInfo = reinterpret_cast< WorkerInfo* >( threadMap->find( threadId )->second );
		int numThreads = (int) threadMap->size();
		if( g_numChildren == 0 )
			cout << threadInfo->workerHostString << " is initialized." << endl;
		simplethread_unlock_mutex( &resourceMutex );

		bool done;
		Timer executionTime(true);
		float totalExecutionTime = 0;
		float lastUpdate = 0;

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

			threadInfo->endIndex = ( spectra.size() / g_numWorkers )-1;

			//cout << threadInfo->workerHostString << " " << numProteins << " " << g_numWorkers << endl;

			Spectrum* s;
			SpectraList::iterator sItr = spectra.begin();
			for(	advance_to_bound( sItr, spectra.end(), threadInfo->workerNum );
					sItr != spectra.end();
					advance_to_bound( sItr, spectra.end(), g_numWorkers ) )
			{
				s = (*sItr);
				++ threadInfo->stats.numSpectraTagged;

				//s->DetermineSpectrumChargeState();
				START_PROFILER(0)
				s->Preprocess();
				STOP_PROFILER(0)

				if( (int) (*sItr)->peakPreData.size() < g_rtConfig->minIntensityClassCount )
					continue;

				START_PROFILER(1)
				threadInfo->stats.numResidueMassGaps += s->MakeTagGraph();
				STOP_PROFILER(1)

				s->MakeProbabilityTables();

				//s->tagGraphs.clear();
				//s->nodeSet.clear();
				deallocate(s->nodeSet);

				START_PROFILER(2)
				threadInfo->stats.numTagsGenerated += s->Score();
				STOP_PROFILER(2)

				threadInfo->stats.numTagsRetained += s->tagList.size();

				//s->gapMaps.clear();
				//s->tagGraphs.clear();
				deallocate(s->gapMaps);
				deallocate(s->tagGraphs);
				if( ( !g_rtConfig->MakeSpectrumGraphs && g_rtConfig->InlineValidationFile.empty() ) )
				{
					//s->peakPreData.clear();
					//s->peakData.clear();
					deallocate(s->peakPreData);
					deallocate(s->peakData);
				}

				if( g_numChildren == 0 )
					totalExecutionTime = executionTime.TimeElapsed();

				if( g_numChildren == 0 && ( ( totalExecutionTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || s->id == ((*spectra.rbegin())->id) ) )
				{
					//int curSpectrum = ( i + 1 ) / g_numWorkers;
					float spectraPerSec = float( threadInfo->stats.numSpectraTagged ) / totalExecutionTime;
					float estimatedTimeRemaining = float( spectra.size() - threadInfo->stats.numSpectraTagged ) / spectraPerSec / numThreads;

					simplethread_lock_mutex( &resourceMutex );
					cout << threadInfo->workerHostString << " has sequence tagged " << threadInfo->stats.numSpectraTagged << " of " << spectra.size() <<
							" spectra; " << spectraPerSec << " per second, " << totalExecutionTime << " elapsed, " << estimatedTimeRemaining << " remaining." << endl;
					cout << threadInfo->workerHostString << " stats: " << 1 << " / " <<
							threadInfo->stats.numSpectraTagged << " / " <<	
							threadInfo->stats.numResidueMassGaps << " / " <<
							threadInfo->stats.numTagsGenerated << " / " <<
							threadInfo->stats.numTagsRetained << endl;

					PRINT_PROFILERS( cout, threadInfo->workerHostString + " profiling" )

					simplethread_unlock_mutex( &resourceMutex );

					lastUpdate = totalExecutionTime;
				}
			}

		}

		return 0;
	}
}
}

namespace std {
	ostream& operator<< ( ostream& o, const list< freicore::directag::Spectrum* >::iterator& itr )
	{
		return o << "itr";//*itr;
	}
}

namespace freicore {
namespace directag {
	taggingStats ExecutePipeline()
	{
		WorkerThreadMap workerThreads;
		int numProcessors = g_numWorkers;

		//if( !g_rtConfig->UseMultipleProcessors )
		//	numProcessors = 1;

		int numSpectra = (int) spectra.size();

		float maxPeakSpace = 0;
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
			if( (*sItr)->totalPeakSpace > maxPeakSpace )
				maxPeakSpace = (*sItr)->totalPeakSpace;

		//cout << "Resizing lnTable to " << maxPeakSpace << endl;
		g_lnFactorialTable.resize( (int) ceil( maxPeakSpace ) );

		if( g_rtConfig->UseMultipleProcessors && g_numWorkers > 1 )
		{
			g_numWorkers = min( numSpectra, g_numWorkers * g_rtConfig->ThreadCountMultiplier );

			for( int i=0; i < g_numWorkers; ++i )
				workerNumbers.push_back(i);

			simplethread_handle_array_t workerHandles;

			simplethread_lock_mutex( &resourceMutex );
			for( int t = 0; t < numProcessors; ++t )
			{
				simplethread_id_t threadId;
				simplethread_handle_t threadHandle = simplethread_create_thread( &threadId, &ExecutePipelineThread, &workerThreads );
				workerThreads[ threadId ] = new WorkerInfo( t, 0, 0 );
				workerHandles.array.push_back( threadHandle );
			}
			simplethread_unlock_mutex( &resourceMutex );

			simplethread_join_all( &workerHandles );
			//cout << g_hostString << " tagged " << numSearched << " of " << spectra.size() << " spectra." << endl;

		} else
		{
			//cout << g_hostString << " is preparing " << numSpectra << " unprepared spectra." << endl;
			g_numWorkers = 1;
			workerNumbers.push_back(0);
			simplethread_id_t threadId = simplethread_get_id();
			workerThreads[ threadId ] = new WorkerInfo( 0, 0, 0 );
			ExecutePipelineThread( &workerThreads );
		}

		taggingStats stats;

		for( WorkerThreadMap::iterator itr = workerThreads.begin(); itr != workerThreads.end(); ++itr )
			stats = stats + reinterpret_cast< WorkerInfo* >( itr->second )->stats;

		g_numWorkers = numProcessors;

		return stats;
	}

	int InitProcess( argList_t& args )
	{
		//cout << g_hostString << " is initializing." << endl;
		if( g_pid == 0 )
		{
          cout << "DirecTag " << Version::str() << " (" << Version::LastModified() << ")\n" <<
                  "FreiCore " << freicore::Version::str() << " (" << freicore::Version::LastModified() << ")\n" <<
                  "ProteoWizard MSData " << pwiz::msdata::Version::str() << " (" << pwiz::msdata::Version::LastModified() << ")\n" <<
                  "ProteoWizard Proteome " << pwiz::proteome::Version::str() << " (" << pwiz::proteome::Version::LastModified() << ")\n" <<
                  DIRECTAG_LICENSE << endl;
		}

		proteinStore proteins;

		g_residueMap = new ResidueMap;
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
				args.erase( args.begin()+i );
			} else if( args[i] == "-cpus" && i+1 <= args.size() )
			{
				g_numWorkers = atoi( args[i+1].c_str() );
				args.erase( args.begin()+i );
			} else
				continue;
			args.erase( args.begin()+i );
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

			if( args.size() < 2 )
			{
				cout << "Not enough arguments.\nUsage: " << args[0] << " <input spectra filemask 1> [input spectra filemask 2] ..." << endl;
				return 1;
			}

			if( !g_rtConfig->initialized() )
			{
				if( g_rtConfig->initializeFromFile() )
				{
					cerr << g_hostString << " could not find the default configuration file (hard-coded defaults in use)." << endl;
				}
				//return 1;
			}

			if( !g_residueMap->initialized() )
			{
				if( g_residueMap->initializeFromFile() )
				{
					cerr << g_hostString << " could not find the default residue masses file (hard-coded defaults in use)." << endl;
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
				if( args[i] == varName && i+1 <= args.size() )
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

	void ReadInlineValidationFile()
	{
		/*if( !g_rtConfig->InlineValidationFile.empty() )
		{
			g_rtConfig->inlineValidationResidues = *g_residueMap;
			fileList_t sqtFilenames;

			if( g_pid == 0 ) cout << "Finding SQT files matching mask \"" << g_rtConfig->InlineValidationFile << "\"" << endl;
			FindFilesByMask( g_rtConfig->InlineValidationFile, sqtFilenames );

			if( sqtFilenames.empty() )
			{
				if( g_pid == 0 ) cerr << "No files found matching given filemasks." << endl;
			} else
			{
				// Read SQT files for validation
				RunTimeVariableMap varsFromFile( "NumChargeStates DynamicMods StaticMods UseAvgMassOfSequences" );
				for( fileList_t::iterator fItr = sqtFilenames.begin(); fItr != sqtFilenames.end(); ++fItr )
				{
					if( g_pid == 0 ) cout << "Reading peptide identifications from \"" << *fItr << "\"" << endl;
					spectra.readSQT( *fItr, true, true, g_rtConfig->ValidationMode, " ", varsFromFile );
					cout << "Setting DynamicMods and StaticMods from SQT file: " << varsFromFile["DynamicMods"] << "; " << varsFromFile["StaticMods"] << endl;
					g_rtConfig->inlineValidationResidues.setDynamicMods( varsFromFile["DynamicMods"] );
					g_rtConfig->inlineValidationResidues.setStaticMods( varsFromFile["StaticMods"] );
					varsFromFile.erase( "DynamicMods" );
					varsFromFile.erase( "StaticMods" );
					g_rtConfig->setVariables( varsFromFile );
				}

				if( spectra.empty() )
				{
					if( g_pid == 0 ) cout << "No identifications found." << endl;

				} else
				{
					if( g_pid == 0 ) cout << "Finished reading " << spectra.size() << " identifications, now calculating validation thresholds." << endl;

					if( g_rtConfig->StartSpectraScanNum == 0 && g_rtConfig->EndSpectraScanNum == -1 )
					{
						spectra.calculateValidationThresholds(	g_rtConfig->scoreThresholds,
																g_rtConfig->NumChargeStates,
																g_rtConfig->Confidence,
																g_rtConfig->DecoyRatio,
																g_rtConfig->DecoyPrefix,														
																g_rtConfig->ValidationMode );

						//SpectraList originalSpectra = spectra;
						vector< size_t > potentialMatchCounts, validMatchCounts;
						pair< SpectraList, SpectraList > filteredSpectra = spectra.filterByThresholds(	g_rtConfig->scoreThresholds,
																										g_rtConfig->NumChargeStates,
																										g_rtConfig->ValidationMode,
																										&potentialMatchCounts,
																										&validMatchCounts );

						for(	SpectraList::ListIndexIterator itr = spectra.index.begin();
								itr != spectra.index.end();
								++itr )
						{
								if( g_rtConfig->InlineValidationMode == TAG_ONLY_HITS &&
									filteredSpectra.first.index.find( itr->first ) == filteredSpectra.first.index.end() )
								{
									deallocate( (*itr->second)->peakPreData );
									deallocate( (*itr->second)->peakData );
								} else if(	g_rtConfig->InlineValidationMode == TAG_ONLY_MISSES &&
											filteredSpectra.second.index.find( itr->first ) == filteredSpectra.first.index.end() )
								{
									deallocate( (*itr->second)->peakPreData );
									deallocate( (*itr->second)->peakData );
								}
						}

						spectra.filterByPeakCount();
						//spectra = originalSpectra;

						for( int z=0; z < g_rtConfig->NumChargeStates; ++z )
						{
							if( g_pid == 0 ) cout << "Threshold for " << g_rtConfig->Confidence * 100.0f << "% confidence in +" << z+1 << " IDs: " <<
									g_rtConfig->scoreThresholds[z] << "; " << validMatchCounts[z] << " of " << potentialMatchCounts[z] << " IDs pass." << endl;
						}

						if( g_pid == 0 ) cout << "All charge states: " << accumulate( validMatchCounts.begin(), validMatchCounts.end(), 0 ) <<
								" of " << accumulate( potentialMatchCounts.begin(), potentialMatchCounts.end(), 0 ) << " IDs pass." << endl;
						//cout << spectra.size() << " identifications pass confidence filter." << endl;
					}
				}
			}
		}*/
	}

	int ProcessHandler( int argc, char* argv[] )
	{
		simplethread_create_mutex( &resourceMutex );

		vector< string > args;
		for( int i=0; i < argc; ++i )
			args.push_back( argv[i] );

		if( InitProcess( args ) )
			return 1;

		SpectraList::InitMzFEBins();
		SpectraList::InitCEBins();

		INIT_PROFILERS(10)

		if( g_pid == 0 )
		{
			// The root process parses the XML data file and distributes spectra to child processes
			g_inputFilenames.clear();
			for( size_t i=1; i < args.size(); ++i )
			{
				cout << g_hostString << " is reading spectra from files matching mask \"" << args[i] << "\"" << endl;
				FindFilesByMask( args[i], g_inputFilenames );
			}

			if( g_inputFilenames.empty() )
			{
				cerr << g_hostString << " did not find any spectra matching given filemasks." << endl;
				return 1;
			}

			Timer overallTime(true);
			fileList_t finishedFiles;
			fileList_t::iterator fItr;
			for( fItr = g_inputFilenames.begin(); fItr != g_inputFilenames.end(); ++fItr )
			{
				spectra.clear();

				Timer fileTime(true);

				cout << g_hostString << " is reading spectra from file \"" << *fItr << "\"" << endl;
				finishedFiles.insert( *fItr );

				Timer readTime(true);
				spectra.readPeaks( *fItr, g_rtConfig->StartSpectraScanNum, g_rtConfig->EndSpectraScanNum );
				readTime.End();

				int totalPeakCount = 0;
				int numSpectra = (int) spectra.size();
				for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
					totalPeakCount += (*sItr)->peakPreCount;

				cout << g_hostString << " read " << numSpectra << " spectra with " << totalPeakCount << " peaks; " << readTime.TimeElapsed() << " seconds elapsed." << endl;

				int skip = 0;
				if( numSpectra == 0 )
				{
					cout << g_hostString << " is skipping a file with no spectra." << endl;
					skip = 1;
				}
				#ifdef USE_MPI
					for( int p=0; p < g_numChildren; ++p )
						MPI_Ssend( &skip,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );
				#endif

				Timer taggingTime;
				string startDate;
				string startTime;
				vector< size_t > opcs; // original peak count statistics
				vector< size_t > fpcs; // filtered peak count statistics

				if( !skip )
				{
					if( g_numProcesses > 1 )
					{
						#ifdef USE_MPI
							if( g_numChildren > 0 )
							{
								g_rtConfig->SpectraBatchSize = (int) ceil( (float) numSpectra / (float) g_numChildren / (float) g_rtConfig->NumBatches );
								cout << g_hostString << " calculates dynamic spectra batch size is " << g_rtConfig->SpectraBatchSize << endl;
							}

							//std::random_shuffle( spectra.begin(), spectra.end() );

							cout << g_hostString << " is sending spectra to worker nodes to prepare them for search." << endl;
							Timer prepareTime(true);
							TransmitUnpreparedSpectraToChildProcesses();

							deallocate( spectra );

							ReceivePreparedSpectraFromChildProcesses();

							SpectraList::PrecacheIRBins( spectra );

							numSpectra = (int) spectra.size();

							skip = 0;
							if( numSpectra == 0 )
							{
								cout << g_hostString << " is skipping a file with no suitable spectra." << endl;
								skip = 1;
							}

							for( int p=0; p < g_numChildren; ++p )
								MPI_Ssend( &skip,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );

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

								cout << g_hostString << " has " << numSpectra << " spectra prepared now; " << prepareTime.End() << " seconds elapsed." << endl;

								ReadInlineValidationFile();

								cout << g_hostString << " is sending " << spectra.size() << " spectra to worker nodes for sequence tagging." << endl;
								startTime = GetTimeString(); startDate = GetDateString(); taggingTime.Begin();
								TransmitUntaggedSpectraToChildProcesses();
								cout << g_hostString << " has sequence tagged all its spectra; " << taggingTime.End() << " seconds elapsed." << endl;

								deallocate( spectra );

								cout << g_hostString << " is receiving tag results from worker nodes." << endl;
								Timer resultsTime(true);
								ReceiveTaggedSpectraFromChildProcesses();
								cout << g_hostString << " finished receiving tag results for " << spectra.size() << " spectra; " << resultsTime.End() << " seconds elapsed." << endl;
							}

						#endif
					} else
					{
						spectra.random_shuffle();

						PrepareSpectra();

						skip = 0;
						if( spectra.size() == 0 )
						{
							cout << g_hostString << " is skipping a file with no suitable spectra." << endl;
							skip = 1;
						}

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

							ReadInlineValidationFile();

							SpectraList::PrecacheIRBins( spectra );

							cout << g_hostString << " is sequence tagging " << spectra.size() << " spectra." << endl;
							startTime = GetTimeString(); startDate = GetDateString(); taggingTime.Begin();
							taggingStats sumTaggingStats = ExecutePipeline();
							//EncodeSpectraForOutput();

							cout << g_hostString << " has sequence tagged all its spectra; " << taggingTime.End() << " seconds elapsed." << endl;

							cout << g_hostString << " stats: " << 1 << " / " <<
									sumTaggingStats.numSpectraTagged << " / " <<
									sumTaggingStats.numResidueMassGaps << " / " <<
									sumTaggingStats.numTagsGenerated << " / " <<
									sumTaggingStats.numTagsRetained << endl;
						}
					}

					// Code for ScanRanker, write metrics and high quality spectra
					if( !skip && (g_rtConfig->WriteScanRankerMetrics || g_rtConfig->WriteHighQualSpectra))
					{
						CalculateQualScore( spectra );
						spectra.sort( spectraSortByQualScore() );
					}

					if( !skip && g_rtConfig->WriteScanRankerMetrics)
					{
						try
						{
						WriteSpecQualMetrics( *fItr, spectra, g_rtConfig->ScanRankerMetricsFileName);
						cout << g_hostString << " finished writing spectral quality metrics for file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
						} catch( ... )
						{
							cerr << "Error while writing ScanRanker metrics file." << endl;
							exit(1);
						}
					}

					if( !skip && g_rtConfig->WriteHighQualSpectra)
					{						
						Spectrum* s;
						mergedSpectraIndices.clear();
						highQualSpectraIndices.clear();
						for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
						{
							s = *sItr;
							// merge duplicate spectra
							vector<int>::iterator found = find(mergedSpectraIndices.begin(),mergedSpectraIndices.end(), s->id.index );
							if( found == mergedSpectraIndices.end() )
							{
								mergedSpectraIndices.push_back( s->id.index );
							}
						}
						int maxOutput = (int) (((double) mergedSpectraIndices.size()) * g_rtConfig->HighQualSpecCutoff);
						cout << endl << "Extracting high quality spectra ..." << endl;
						cout << "The number of filtered spectra: " << mergedSpectraIndices.size() << endl;
						cout << "The number of high quality spectra: " << maxOutput << endl;
						for(int i = 0; i < maxOutput; ++i )
						{
							highQualSpectraIndices.push_back( mergedSpectraIndices.at(i) );
						}

						try
						{
							std::sort( highQualSpectraIndices.begin(), highQualSpectraIndices.end() );
							writeHighQualSpectra( *fItr, highQualSpectraIndices, g_rtConfig->OutputFormat, g_rtConfig->HighQualSpecFileName);
							cout << g_hostString << " finished writing high quality spectra for file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;

						} catch( ... )
						{
							cerr << "Error while sorting and writing output." << endl;
							exit(1);
						}
					}

					if( !skip && g_rtConfig->WriteOutTags)
					{
						try
						{
							spectra.sort( spectraSortByID() );
							WriteTagsToTagsFile( *fItr, startTime, startDate, taggingTime.End() );
							cout << g_hostString << " finished file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
						} catch( ... )
						{
							cerr << "Error while sorting and writing XML output." << endl;
							exit(1);
						}
					}

					//if( !skip )
					//{
					//	try
					//	{
					//		spectra.sort( spectraSortByID() );
					//		WriteTagsToTagsFile( *fItr, startTime, startDate, taggingTime.End() );
					//		cout << g_hostString << " finished file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
					//	} catch( ... )
					//	{
					//		cerr << "Error while sorting and writing XML output." << endl;
					//		exit(1);
					//	}
					//}
				}
			}

			#ifdef USE_MPI
					int done = ( ( g_inputFilenames.size() - finishedFiles.size() ) == 0 ? 1 : 0 );
					for( int p=0; p < g_numChildren; ++p )
						MPI_Ssend( &done,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );
			#endif

			cout << g_hostString << " sequence tagged spectra from " << g_inputFilenames.size() << " files; " << overallTime.End() << " seconds elapsed." << endl;
		}
		#ifdef USE_MPI
			else
			{
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

						TransmitPreparedSpectraToRootProcess( preparedSpectra );
						deallocate( preparedSpectra );

						MPI_Recv( &skip,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );

						if( !skip )
						{
							SpectraList taggedSpectra;
							SpectraList::PrecacheIRBins( spectra );

							int numBatches = 0;
							taggingStats sumTaggingStats;
							taggingStats lastTaggingStats;
							while( ReceiveUntaggedSpectraBatchFromRootProcess() )
							{
								++ numBatches;

								lastTaggingStats = ExecutePipeline();
								sumTaggingStats = sumTaggingStats + lastTaggingStats;

								taggedSpectra.insert( spectra.begin(), spectra.end(), taggedSpectra.end() );
								spectra.clear( false );
							}

							cout << g_hostString << " stats: " << numBatches << " / " <<
									sumTaggingStats.numSpectraTagged << " / " <<
									sumTaggingStats.numResidueMassGaps << " / " <<
									sumTaggingStats.numTagsGenerated << " / " <<
									sumTaggingStats.numTagsRetained << endl;

							TransmitTaggedSpectraToRootProcess( taggedSpectra );
							taggedSpectra.clear();
						}
					}

					MPI_Recv( &allDone,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );
				}
			}
		#endif

		return 0;
	}

	gapMap_t::iterator FindPeakNear( gapMap_t& peakData, float mz, float tolerance )
	{
		gapMap_t::iterator cur, min, max, best;

		min = peakData.lower_bound( mz - tolerance );
		max = peakData.lower_bound( mz + tolerance );

		if( min == max )
			return peakData.end(); // no peaks

		// find the peak closest to the desired mz
		best = min;
		float minDiff = (float) fabs( mz - best->first );
		for( cur = min; cur != max; ++cur )
		{
			float curDiff = (float) fabs( mz - cur->first );
			if( curDiff < minDiff )
			{
				minDiff = curDiff;
				best = cur;
			}
		}

		return best;
	}
}
}

int main( int argc, char* argv[] )
{
	try
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
		#ifdef MPI_DEBUG
			cout << g_hostString << " is starting." << endl;
		#endif

		int result = directag::ProcessHandler( argc, argv );

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

	} catch( exception& e )
	{
		cerr << e.what() << endl;
	}

	return 1;
}
