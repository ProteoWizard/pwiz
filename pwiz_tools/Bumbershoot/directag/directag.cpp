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
#include "scanRanker.h"
#include "Histogram.h"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/data/proteome/Version.hpp"
#include "directagVersion.hpp"
#include <boost/lockfree/fifo.hpp>
#include <boost/thread/thread.hpp>
#include <boost/thread/mutex.hpp>

//#include "ranker.h"
//#include "writeHighQualSpectra.h"

using namespace freicore;

namespace freicore
{
namespace directag
{
    SpectraList                         spectra;
    boost::lockfree::fifo<Spectrum*>    taggingTasks;
    TaggingStatistics                   taggingStatistics;
    PeakFilteringStatistics             peakStatistics;
    shared_ptr<RunTimeConfig>           rtConfig;

	void WriteTagsToTagsFile(	const string& inputFilename,
								string startTime,
								string startDate,
								float totalTaggingTime )
	{
		string filenameAsScanName;
		filenameAsScanName =	inputFilename.substr( inputFilename.find_last_of( SYS_PATH_SEPARATOR )+1,
								inputFilename.find_last_of( '.' ) - inputFilename.find_last_of( SYS_PATH_SEPARATOR )-1 );
		string outputFilename = filenameAsScanName + rtConfig->OutputSuffix + ".tags";

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
					"H\tUsed " << g_numProcesses << " processing " << ( g_numProcesses > 1 ? "nodes" : "node" ) << ".\n";

		cout << "Writing tags to \"" << outputFilename << "\"." << endl;
		spectra.writeTags( inputFilename, rtConfig->OutputSuffix, header.str(), rtConfig->getVariables() );
		spectra.clear();
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
				if( !rtConfig->UseChargeStateFromMS )
					spectra.setId( s->id, SpectrumId( s->id.nativeID, 0 ) );

				if( s->id.charge == 0 )
				{
					SpectrumId preChargeId( s->id );
					s->DetermineSpectrumChargeState();
					SpectrumId postChargeId( s->id );

					if( postChargeId.charge == 0 )
					{
						postChargeId.setCharge(2);

						if( rtConfig->DuplicateSpectra )
						{
							for( int z = 3; z <= rtConfig->NumChargeStates; ++z )
							{
								Spectrum* s2 = new Spectrum( *s );
								s2->id.setCharge(z);
                                s2->setTagConfig(rtConfig);
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
	}

	vector< int > workerNumbers;
	int numSearched;

    void ExecuteSequenceTagger()
	{
        try
        {
            Spectrum* taggingTask;
	        while( true )
	        {
                if (!taggingTasks.dequeue(&taggingTask))
			        break;

			    Spectrum* s = taggingTask;
                s->processAndTagSpectrum(peakStatistics, taggingStatistics, true);
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
            workerThreads.push_back(workerThreadGroup.create_thread(&ExecuteSequenceTagger));

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
                if (!workerThreads[i]->timed_join(bpt::seconds(round(rtConfig->StatusUpdateFrequency))))
                    --i;

                bpt::ptime current = bpt::microsec_clock::local_time();

                // only make one update per StatusUpdateFrequency seconds
                if ((current - lastUpdate).total_microseconds() / 1e6 < rtConfig->StatusUpdateFrequency)
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
		rtConfig = shared_ptr<RunTimeConfig>(new RunTimeConfig);
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
                if( rtConfig->initializeFromFile( args[i+1] ) )
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

        if( !rtConfig->initialized() )
        {
            if( rtConfig->initializeFromFile() )
                cerr << "Could not find the default configuration file (hard-coded defaults in use)." << endl;
        }

        if( !g_residueMap->initialized() )
        {
            cerr << "Failed to initialize residue masses." << endl;
            return 1;
        }

		// Command line overrides happen after config file has been distributed but before PTM parsing
		RunTimeVariableMap vars = rtConfig->getVariables();
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
		rtConfig->setVariables( vars );

        for( size_t i=1; i < args.size(); ++i )
        {
            if( args[i] == "-dump" )
            {
                rtConfig->dump();
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

        rtConfig->PreComputeScoreDistributions();

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
            spectra.readPeaks( *fItr, 0, -1, 2, rtConfig->SpectrumListFilters, rtConfig->NumChargeStates );
            readTime.End();

            int totalPeakCount = 0;
            int numSpectra = (int) spectra.size();
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
            {
                totalPeakCount += (*sItr)->peakPreCount;
                (*sItr)->setTagConfig(rtConfig);
            }

            cout << "Read " << numSpectra << " spectra with " << totalPeakCount << " peaks; " << readTime.TimeElapsed() << " seconds elapsed." << endl;

            if( numSpectra == 0 )
            {
                cout << "Skipping a file with no spectra." << endl;
                continue;
            }

            Timer taggingTime;
            string startDate;
            string startTime;

            PrepareSpectra();
            spectra.random_shuffle();

            if( spectra.size() == 0 )
            {
                cout << "Skipping a file with no suitable spectra." << endl;
                continue;
            }

            cout << "Sequence tagging " << spectra.size() << " spectra." << endl;
            startTime = GetTimeString(); startDate = GetDateString(); taggingTime.Begin();
            ExecutePipeline();
            cout << "Finished sequence tagging spectra; " << taggingTime.End() << " seconds elapsed." << endl;
            cout << (string) peakStatistics << endl;
            cout << "Overall stats: " << (string) taggingStatistics << endl;
            peakStatistics.reset();
            taggingStatistics.reset();

            // Code for ScanRanker, write metrics and high quality spectra
            if( rtConfig->WriteScanRankerMetrics || rtConfig->WriteHighQualSpectra )
            {
                CalculateQualScore( spectra );
                spectra.sort( spectraSortByQualScore() );
            }

            if( rtConfig->WriteScanRankerMetrics )
            {
                try
                {
                    WriteSpecQualMetrics( *fItr, spectra, rtConfig->ScanRankerMetricsFileName);
                    cout << "Finished writing spectral quality metrics for file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
                } catch( ... )
                {
                    cerr << "Error while writing ScanRanker metrics file." << endl;
                    exit(1);
                }
            }

            if(  rtConfig->WriteHighQualSpectra )
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
                int maxOutput = (int) (((double) mergedSpectraIndices.size()) * rtConfig->HighQualSpecCutoff);
                cout << endl << "Extracting high quality spectra ..." << endl;
                cout << "The number of filtered spectra: " << mergedSpectraIndices.size() << endl;
                cout << "The number of high quality spectra: " << maxOutput << endl;
                for(int i = 0; i < maxOutput; ++i )
                    highQualSpectraIndices.push_back( mergedSpectraIndices.at(i) );

                try
                {
                    //std::sort( highQualSpectraIndices.begin(), highQualSpectraIndices.end() );
                    writeHighQualSpectra( *fItr, highQualSpectraIndices, rtConfig->OutputFormat, rtConfig->HighQualSpecFileName);
                    cout << "Finished writing high quality spectra for file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;

                } catch( ... )
                {
                    cerr << "Error while sorting and writing output." << endl;
                    exit(1);
                }
            }

            if( rtConfig->WriteOutTags )
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
