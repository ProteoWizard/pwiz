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
// Contributor(s): Surendra Dasari, Zeqiang Ma
//

#include "stdafx.h"
#include "directag.h"
#include "Histogram.h"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/data/proteome/Version.hpp"
#include "directagVersion.hpp"
#include <boost/lockfree/fifo.hpp>
#include <boost/thread/thread.hpp>
#include <boost/thread/mutex.hpp>

//#include "ranker.h"
#include "writeHighQualSpectra.h"

using namespace freicore;

namespace freicore
{
namespace directag
{
    SpectraList                         spectra;
    boost::lockfree::fifo<Spectrum*>    taggingTasks;
    TaggingStatistics                   taggingStatistics;
	map< char, float >                  compositionInfo;

	RunTimeConfig*               g_rtConfig;

	// Code for ScanRanker
	vector<NativeID>		mergedSpectraIndices;
	vector<NativeID>		highQualSpectraIndices;
	float					bestTagScoreMean;
	float					bestTagTICMean;
	float					tagMzRangeMean;
	float					bestTagScoreIQR;
	float					bestTagTICIQR;
	float					tagMzRangeIQR;
	size_t					numTaggedSpectra;


	double lnCombin( int a, int b ) { return lnCombin( a, b, g_lnFactorialTable ); }
	float GetMassOfResidues( const string& a, bool b ) { return g_residueMap->GetMassOfResidues( a, b ); }

	void WriteTagsToTagsFile(	const string& inputFilename,
								string startTime,
								string startDate,
								float totalTaggingTime )
	{
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

		cout << "Writing tags to \"" << outputFilename << "\"." << endl;
		spectra.writeTags( inputFilename, g_rtConfig->OutputSuffix, header.str(), g_rtConfig->getVariables() );
		spectra.clear();
	}

	// Code for writing ScanRanker metrics file
	void WriteSpecQualMetrics( const string& inputFilename, SpectraList& instance, const string& outFilename)
	{
		cout << "Generating output of quality metrics." << endl;
		string filenameAsScanName;
		filenameAsScanName =	inputFilename.substr( inputFilename.find_last_of( SYS_PATH_SEPARATOR )+1,
								inputFilename.find_last_of( '.' ) - inputFilename.find_last_of( SYS_PATH_SEPARATOR )-1 );

		string outputFilename = (outFilename.empty()) ? (filenameAsScanName + "-ScanRankerMetrics" + ".txt") :  outFilename;
		
		ofstream fileStream( outputFilename.c_str() );

		fileStream << "H\tBestTagScoreMean\tBestTagTICMean\tTagMzRangeMean\tBestTagScoreIQR\tBestTagTICIQR\tTagMzRangeIQR\tnumTaggedSpectra\n";
		fileStream 	<< "H"<< '\t'
					<< bestTagScoreMean << '\t'
					<< bestTagTICMean << '\t'
					<< tagMzRangeMean << '\t'
					<< bestTagScoreIQR << '\t'
					<< bestTagTICIQR << '\t'
					<< tagMzRangeIQR << '\t'
					<< numTaggedSpectra << "\n";
		//fileStream << "H\tIndex\tNativeID\tPrecursorMZ\tCharge\tPrecursorMass\tBestTagScore\tBestTagTIC\tTagMzRange\tScanRankerScore\n" ;
		fileStream << "H\tNativeID\tPrecursorMZ\tCharge\tPrecursorMass\tBestTagScore\tBestTagTIC\tTagMzRange\tScanRankerScore\n" ;
		set<NativeID> seen;
		Spectrum* s;
		for( SpectraList::iterator sItr = instance.begin(); sItr != instance.end(); ++sItr )
		{
			s = *sItr;
			float logBestTagTIC = (s->bestTagTIC == 0) ? 0 : (log( s->bestTagTIC ));
            pair<set<NativeID>::iterator, bool> insertResult = seen.insert(s->id.nativeID);
			if( insertResult.second ) // only write out metrics of best scored spectrum if existing multiple charge states
			{
				fileStream	<< "S" << '\t'
							//<< s->nativeID << '\t'
							<< s->nativeID << '\t'
							<< s->mzOfPrecursor << '\t'
							<< s->id.charge << '\t'
							<< s->mOfPrecursor << '\t'
							<< s->bestTagScore << '\t'
							<< logBestTagTIC << '\t'
							<< s->tagMzRange << '\t'
							//<< s->bestTagScoreNorm << '\t'
							//<< s->bestTagTICNorm << '\t'
							//<< s->tagMzRangeNorm << '\t'
							<< s->qualScore << '\n';
			}
		}
	}

	// Code for calculating ScanRanker score
	void CalculateQualScore( SpectraList& instance)
	{
		vector<float> bestTagScoreList;
		vector<float> bestTagTICList;
		vector<float> tagMzRangeList;
		//vector<float> rankedBestTagScoreList;
		//vector<float> rankedBestTagTICList;
		//vector<float> rankedTagMzRangeList;

		Spectrum* s;
		// string rankMethod = "average"; //Can also be "min" or "max" or "default"
	
		// use log transformed mean and IQR of spectra with at least 1 tag for normalization
		for( SpectraList::iterator sItr = instance.begin(); sItr != instance.end(); ++sItr )
		{
			s = *sItr;
			if ( s->bestTagScore != 0 )    // at least 1 tag generated and <= MaxTagScore
			{  
				bestTagScoreList.push_back( s->bestTagScore );  // bestTagScore is the chisqured value
				bestTagTICList.push_back( log( s->bestTagTIC ));
				tagMzRangeList.push_back( s->tagMzRange );
			}
		}

		//rankhigh( bestTagScoreList, rankedBestTagScoreList, rankMethod );
		//rank( bestTagTICList, rankedBestTagTICList, rankMethod );
		//rank( tagMzRangeList, rankedTagMzRangeList, rankMethod );

		float bestTagScoreSum = accumulate( bestTagScoreList.begin(), bestTagScoreList.end(), 0.0 );
		float bestTagTICSum = accumulate( bestTagTICList.begin(), bestTagTICList.end(), 0.0 );
		float tagMzRangeSum = accumulate( tagMzRangeList.begin(), tagMzRangeList.end(), 0.0 );

		std::sort( bestTagScoreList.begin(), bestTagScoreList.end() );
		bestTagScoreIQR = bestTagScoreList[(int)(bestTagScoreList.size() * 0.75)] - bestTagScoreList[(int)(bestTagScoreList.size() * 0.25)];
		std::sort( bestTagTICList.begin(), bestTagTICList.end() );
		bestTagTICIQR = bestTagTICList[(int)(bestTagTICList.size() * 0.75)] - bestTagTICList[(int)(bestTagTICList.size() * 0.25)];
		std::sort( tagMzRangeList.begin(), tagMzRangeList.end() );
		tagMzRangeIQR = tagMzRangeList[(int)(tagMzRangeList.size() * 0.75)] - tagMzRangeList[(int)(tagMzRangeList.size() * 0.25)];
		//int i = 0;
		//size_t numSpectra = instance.size();
		numTaggedSpectra = bestTagScoreList.size();
		bestTagScoreMean = bestTagScoreSum / (float) numTaggedSpectra;
		bestTagTICMean = bestTagTICSum / (float) numTaggedSpectra;
		tagMzRangeMean = tagMzRangeSum / (float) numTaggedSpectra;
		
		for( SpectraList::iterator sItr = instance.begin(); sItr != instance.end(); ++sItr )
		{
			s = *sItr;
			s->bestTagScoreNorm = (s->bestTagScore - bestTagScoreMean) / bestTagScoreIQR;
			s->bestTagTICNorm = ( s->bestTagScore == 0 ) ? ( 0 - bestTagTICMean) / bestTagTICIQR : (log( s->bestTagTIC ) - bestTagTICMean) / bestTagTICIQR;
			s->tagMzRangeNorm = ( s->bestTagScore == 0 ) ? ( 0 - tagMzRangeMean) / tagMzRangeIQR : ( s->tagMzRange - tagMzRangeMean) /tagMzRangeIQR;

//			s->bestTagScoreNorm = (rankedBestTagScoreList[i]-1) / (float) numTotalSpectra;
//			s->bestTagTICNorm = (rankedBestTagTICList[i]-1) / (float) numTotalSpectra;
//			s->tagMzRangeNorm = (rankedTagMzRangeList[i]-1) / (float) numTotalSpectra;
//          s->qualScore = ( rankedBestTagScoreList[i] + rankedBestTagTICList[i] + rankedTagMzRangeList[i] ) / (3 * (float) numTotalSpectra);
			s->qualScore = (s->bestTagScoreNorm + s->bestTagTICNorm + s->tagMzRangeNorm ) / 3;
			//++i;
		}
	}

	void PrepareSpectra()
	{
		Timer timer;

		cout << "Trimming spectra with less than " << 10 << " peaks." << endl;

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

		cout << "Trimmed " << preTrimCount << " spectra for being too small before peak filtering." << endl;
		cout << "Determining spectrum charge states from " << spectra.size() << " spectra." << endl;

		timer.Begin();
		SpectraList duplicates;
		BOOST_FOREACH(Spectrum* s, spectra)
		{
			try
			{
				if( !g_rtConfig->UseChargeStateFromMS )
					spectra.setId( s->id, SpectrumId( s->id.nativeID, 0 ) );

				if( s->id.charge == 0 )
				{
					SpectrumId preChargeId( s->id );
					s->DetermineSpectrumChargeState();
					SpectrumId postChargeId( s->id );

					if( postChargeId.charge == 0 )
					{
						postChargeId.setCharge(2);

						if( g_rtConfig->DuplicateSpectra )
						{
							for( int z = 3; z <= g_rtConfig->NumChargeStates; ++z )
							{
								Spectrum* s2 = new Spectrum( *s );
								s2->id.setCharge(z);
								duplicates.push_back(s2);
							}
						}
					}

					spectra.setId( preChargeId, postChargeId );
				}

			} catch( exception& e )
			{
				throw runtime_error( string( "duplicating scan " ) + string( s->id ) + ": " + e.what() );
			} catch( ... )
			{
				throw runtime_error( string( "duplicating scan " ) + string( s->id ) );
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

		cout << "Finished determining spectrum charge states; " << timer.End() << " seconds elapsed." << endl;
		cout << "Filtering peaks in " << spectra.size() << " spectra." << endl;

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

		cout << "Finished filtering peaks; " << timer.End() << " seconds elapsed." << endl;

		int postTrimCount = 0;
		postTrimCount = spectra.filterByPeakCount( g_rtConfig->minIntensityClassCount );

		cout << "Trimmed " << postTrimCount << " spectra for being too small after peak filtering." << endl;
	}

	vector< int > workerNumbers;
	int numSearched;

	void ExecutePipelineThread()
	{
        try
        {
            Spectrum* taggingTask;
	        while( true )
	        {
                if (!taggingTasks.dequeue(&taggingTask))
			        break;

			    Spectrum* s = taggingTask;
			    ++ taggingStatistics.numSpectraTagged;

			    //s->DetermineSpectrumChargeState();
			    START_PROFILER(0)
			    s->Preprocess();
			    STOP_PROFILER(0)

			    if( (int) s->peakPreData.size() < g_rtConfig->minIntensityClassCount )
				    continue;

			    START_PROFILER(1)
			    taggingStatistics.numResidueMassGaps += s->MakeTagGraph();
			    STOP_PROFILER(1)

			    s->MakeProbabilityTables();

			    //s->tagGraphs.clear();
			    //s->nodeSet.clear();
			    deallocate(s->nodeSet);

			    START_PROFILER(2)
			    taggingStatistics.numTagsGenerated += s->Score();
			    STOP_PROFILER(2)

			    taggingStatistics.numTagsRetained += s->tagList.size();

			    //s->gapMaps.clear();
			    //s->tagGraphs.clear();
			    deallocate(s->gapMaps);
			    deallocate(s->tagGraphs);
			    if( !g_rtConfig->MakeSpectrumGraphs )
			    {
				    //s->peakPreData.clear();
				    //s->peakData.clear();
				    deallocate(s->peakPreData);
				    deallocate(s->peakData);
			    }
		    }
        } catch( std::exception& e )
        {
            cerr << " terminated with an error: " << e.what() << endl;
        } catch(...)
        {
            cerr << " terminated with an unknown error." << endl;
        }
	}

    void ExecutePipeline()
	{
		int numSpectra = (int) spectra.size();

		float maxPeakSpace = 0;
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
        {
			taggingTasks.enqueue(*sItr);
			if( (*sItr)->totalPeakSpace > maxPeakSpace )
				maxPeakSpace = (*sItr)->totalPeakSpace;
        }

		//cout << "Resizing lnTable to " << maxPeakSpace << endl;
		g_lnFactorialTable.resize( (int) ceil( maxPeakSpace ) );

        bpt::ptime start = bpt::microsec_clock::local_time();

        boost::thread_group workerThreadGroup;
        vector<boost::thread*> workerThreads;

		for (int i = 0; i < g_numWorkers; ++i)
            workerThreads.push_back(workerThreadGroup.create_thread(&ExecutePipelineThread));

        if (g_numChildren > 0)
        {
            // MPI jobs do a simple join_all
            workerThreadGroup.join_all();
        }
        else
        {
            bpt::ptime lastUpdate = start;

            for (int i=0; i < g_numWorkers; ++i)
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

			    float spectraPerSec = static_cast<float>(taggingStatistics.numSpectraTagged) / elapsed.total_microseconds() * 1e6;
                bpt::time_duration estimatedTimeRemaining(0, 0, round((numSpectra - taggingStatistics.numSpectraTagged) / spectraPerSec));

		        cout << "Sequence tagged " << taggingStatistics.numSpectraTagged << " of " << numSpectra << " spectra; "
                     << round(spectraPerSec) << " per second, "
                     << format_date_time("%H:%M:%S", bpt::time_duration(0, 0, elapsed.total_seconds())) << " elapsed, "
                     << format_date_time("%H:%M:%S", estimatedTimeRemaining) << " remaining." << endl;
		    }
        }
	}

	int InitProcess( argList_t& args )
	{
       cout << "DirecTag " << Version::str() << " (" << Version::LastModified() << ")\n" <<
               "FreiCore " << freicore::Version::str() << " (" << freicore::Version::LastModified() << ")\n" <<
               "ProteoWizard MSData " << pwiz::msdata::Version::str() << " (" << pwiz::msdata::Version::LastModified() << ")\n" <<
               "ProteoWizard Proteome " << pwiz::proteome::Version::str() << " (" << pwiz::proteome::Version::LastModified() << ")\n" <<
               DIRECTAG_LICENSE << endl;

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
            cout << "Not enough arguments.\nUsage: " << args[0] << " <input spectra filemask 1> [input spectra filemask 2] ..." << endl;
            return 1;
        }

        if( !g_rtConfig->initialized() )
        {
            if( g_rtConfig->initializeFromFile() )
            {
                cerr << "Could not find the default configuration file (hard-coded defaults in use)." << endl;
            }
            //return 1;
        }

        if( !g_residueMap->initialized() )
        {
            cerr << "Failed to initialize residue masses." << endl;
            return 1;
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

		return 0;
	}

	int ProcessHandler( int argc, char* argv[] )
	{
		vector< string > args;
		for( int i=0; i < argc; ++i )
			args.push_back( argv[i] );

		if( InitProcess( args ) )
			return 1;

        g_rtConfig->PreComputeScoreDistributions();

        INIT_PROFILERS(10)

        // The root process parses the XML data file and distributes spectra to child processes
        g_inputFilenames.clear();
        for( size_t i=1; i < args.size(); ++i )
            FindFilesByMask( args[i], g_inputFilenames );

        if( g_inputFilenames.empty() )
        {
            cerr << "No source files found matching given filemasks." << endl;
            return 1;
        }

        cout << "Found " << g_inputFilenames.size() << " file" << (g_inputFilenames.size()>1?"s ":" ") << "for sequence tagging." << endl;

        Timer overallTime(true);
        fileList_t finishedFiles;
        fileList_t::iterator fItr;
        for( fItr = g_inputFilenames.begin(); fItr != g_inputFilenames.end(); ++fItr )
        {
            spectra.clear();

            Timer fileTime(true);

            cout << "Reading spectra from file \"" << *fItr << "\"" << endl;
            finishedFiles.insert( *fItr );

            Timer readTime(true);
            spectra.readPeaks( *fItr, 0, -1, 2, g_rtConfig->SpectrumListFilters, g_rtConfig->NumChargeStates );
            readTime.End();

            int totalPeakCount = 0;
            int numSpectra = (int) spectra.size();
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
                totalPeakCount += (*sItr)->peakPreCount;

            cout << "Read " << numSpectra << " spectra with " << totalPeakCount << " peaks; " << readTime.TimeElapsed() << " seconds elapsed." << endl;

            if( numSpectra == 0 )
            {
                cout << "Skipping a file with no spectra." << endl;
                continue;
            }

            Timer taggingTime;
            string startDate;
            string startTime;
            vector< size_t > opcs; // original peak count statistics
            vector< size_t > fpcs; // filtered peak count statistics

            spectra.random_shuffle();

            PrepareSpectra();

            if( spectra.size() == 0 )
            {
                cout << "Skipping a file with no suitable spectra." << endl;
                continue;
            }

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
            
            cout << "Sequence tagging " << spectra.size() << " spectra." << endl;
            startTime = GetTimeString(); startDate = GetDateString(); taggingTime.Begin();
            ExecutePipeline();

            cout << "Finished sequence tagging spectra; " << taggingTime.End() << " seconds elapsed." << endl;

            cout << "Overall stats: " << (string) taggingStatistics << endl;
            taggingStatistics.reset();

            // Code for ScanRanker, write metrics and high quality spectra
            if( g_rtConfig->WriteScanRankerMetrics || g_rtConfig->WriteHighQualSpectra )
            {
                CalculateQualScore( spectra );
                spectra.sort( spectraSortByQualScore() );
            }

            if( g_rtConfig->WriteScanRankerMetrics )
            {
                try
                {
                    WriteSpecQualMetrics( *fItr, spectra, g_rtConfig->ScanRankerMetricsFileName);
                    cout << "Finished writing spectral quality metrics for file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
                } catch( ... )
                {
                    cerr << "Error while writing ScanRanker metrics file." << endl;
                    exit(1);
                }
            }

            if(  g_rtConfig->WriteHighQualSpectra )
            {						
                mergedSpectraIndices.clear();
                highQualSpectraIndices.clear();
                set<NativeID> seen;
                BOOST_FOREACH(Spectrum* s, spectra)
                {
                    // merge duplicate spectra with differen charge state
                    pair<set<NativeID>::iterator, bool> insertResult = seen.insert(s->id.nativeID);
                    if( insertResult.second )
                        mergedSpectraIndices.push_back( s->id.nativeID );
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
                    //std::sort( highQualSpectraIndices.begin(), highQualSpectraIndices.end() );
                    writeHighQualSpectra( *fItr, highQualSpectraIndices, g_rtConfig->OutputFormat, g_rtConfig->HighQualSpecFileName);
                    cout << "Finished writing high quality spectra for file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;

                } catch( ... )
                {
                    cerr << "Error while sorting and writing output." << endl;
                    exit(1);
                }
            }

            if( g_rtConfig->WriteOutTags )
            {
                try
                {
                    spectra.sort( spectraSortByID() );
                    WriteTagsToTagsFile( *fItr, startTime, startDate, taggingTime.End() );
                    cout << "Finished file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
                } catch( ... )
                {
                    cerr << "Error while sorting and writing XML output." << endl;
                    exit(1);
                }
            }
        }

        cout << "Finished tagging " << g_inputFilenames.size() << " files; " << overallTime.End() << " seconds elapsed." << endl;

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

		g_numProcesses = 1;
		g_pid = 0;

		g_numChildren = g_numProcesses - 1;

		ostringstream str;
		str << "Process #" << g_pid << " (" << buf << ")";
		g_hostString = str.str();

		int result = directag::ProcessHandler( argc, argv );

		return result;

	} catch( exception& e )
	{
		cerr << e.what() << endl;
	}

	return 1;
}
