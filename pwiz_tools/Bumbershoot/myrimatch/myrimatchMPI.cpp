#define BOOST_LIB_DIAGNOSTIC
#include "stdafx.h"
#include "myrimatch.h"

#ifdef USE_MPI

#include <boost/iostreams/filtering_stream.hpp>
#include <boost/iostreams/copy.hpp>
#include <boost/iostreams/filter/zlib.hpp>

namespace freicore
{
    
MPI_Status		st;
void*			g_mpiBuffer;

namespace myrimatch
{
	void TransmitConfigsToChildProcesses()
	{
		for( int p=0; p < g_numChildren; ++p )
		{
			int len;

			len = (int) g_rtConfig->cfgStr.length();
			MPI_Send( &len,									1,		MPI_INT,			p+1,	0x00, MPI_COMM_WORLD );
			MPI_Send( (void*) g_rtConfig->cfgStr.c_str(),	len,	MPI_CHAR,			p+1,	0x01, MPI_COMM_WORLD );

			len = (int) g_residueMap->cfgStr.length();
			MPI_Send( &len,									1,		MPI_INT,			p+1,	0x02, MPI_COMM_WORLD );
			MPI_Send( (void*) g_residueMap->cfgStr.c_str(),	len,	MPI_CHAR,			p+1,	0x03, MPI_COMM_WORLD );
		}
	}

	void ReceiveConfigsFromRootProcess()
	{
		int len;

		MPI_Recv( &len,										1,		MPI_INT,			0,		0x00, MPI_COMM_WORLD, &st );
		g_rtConfig->cfgStr.resize( len );
		MPI_Recv( &g_rtConfig->cfgStr[0],					len,	MPI_CHAR,			0,		0x01, MPI_COMM_WORLD, &st );
		g_rtConfig->initializeFromBuffer( g_rtConfig->cfgStr, "\r\n#" );

		g_residueMap = new ResidueMap();
		MPI_Recv( &len,										1,		MPI_INT,			0,		0x02, MPI_COMM_WORLD, &st );
		g_residueMap->cfgStr.resize( len );
		MPI_Recv( &g_residueMap->cfgStr[0],					len,	MPI_CHAR,			0,		0x03, MPI_COMM_WORLD, &st );
		g_residueMap->initializeFromBuffer( g_residueMap->cfgStr );
	}

	int ReceivePreparedSpectraFromChildProcesses()
	{
		Timer receiveTime( true );
		float totalReceiveTime = 0.01f;
		float lastUpdate = 0.0f;
		int sourceProcess, numSpectra;
		for( int p=0; p < g_numChildren; ++p )
		{
			MPI_Recv( &sourceProcess,		1,	MPI_INT,	MPI_ANY_SOURCE,	0xEE, MPI_COMM_WORLD, &st );

			#ifdef MPI_DEBUG
				cout << g_hostString << " is receiving " << numSpectra << " prepared spectra." << endl;
				Timer receiveTime(true);
			#endif

			string pack;
			int len;

			MPI_Recv( &len,					1,			MPI_INT,	sourceProcess,	0x00, MPI_COMM_WORLD, &st );
			pack.resize( len );
			MPI_Recv( (void*) pack.data(),	len,		MPI_CHAR,	sourceProcess,	0x01, MPI_COMM_WORLD, &st );

			stringstream compressedStream( pack );
			stringstream packStream;
			boost::iostreams::filtering_ostream decompressorStream;
			decompressorStream.push( boost::iostreams::zlib_decompressor() );
			decompressorStream.push( packStream );
			boost::iostreams::copy( compressedStream, decompressorStream );
			decompressorStream.reset();

			binary_iarchive packArchive( packStream );

			try
			{
				packArchive & numSpectra;

				//cout << g_hostString << " is unpacking results for " << numSpectra << " spectra." << endl;
				for( int j=0; j < numSpectra; ++j )
				{
					Spectrum* s = new Spectrum;
					packArchive & *s;
					spectra.push_back( s );

				}
				//cout << g_hostString << " is finished unpacking results." << endl;
			} catch( exception& e )
			{
				cerr << g_hostString << " had an error: " << e.what() << endl;
				exit(1);
			}

			#ifdef MPI_DEBUG
				cout << g_hostString << " finished receiving " << numSpectra << " prepared spectra; " <<
						receiveTime.End() << " seconds elapsed." << endl;
			#endif

			totalReceiveTime = receiveTime.TimeElapsed();
			if( ( totalReceiveTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || p+1 == g_numChildren )
			{
				float nodesPerSec = float(p+1) / totalReceiveTime;
				float estimatedTimeRemaining = float(g_numChildren-p-1) / nodesPerSec;
				cout << g_hostString << " has received prepared spectra from " << p+1 << " of " << g_numChildren << " worker nodes; " << nodesPerSec <<
						" per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
				lastUpdate = totalReceiveTime;
			}
		}
		return 0;
	}

	int TransmitPreparedSpectraToRootProcess( SpectraList& preparedSpectra )
	{
		int numSpectra = (int) preparedSpectra.size();

		stringstream packStream;
		binary_oarchive packArchive( packStream );

		//Timer packTime(true);
		//cout << g_hostString << " is packing " << numSpectra << " results." << endl;
		packArchive & numSpectra;
		for( SpectraList::iterator sItr = preparedSpectra.begin(); sItr != preparedSpectra.end(); ++sItr )
		{
			Spectrum* s = *sItr;
			packArchive & *s;
		}
		//cout << g_hostString << " finished packing results; " << packTime.End() << " seconds elapsed." << endl;

		MPI_Ssend( &g_pid,		1,	MPI_INT,		0,	0xEE, MPI_COMM_WORLD );

		#ifdef MPI_DEBUG
			cout << g_hostString << " is sending " << numSpectra << " prepared spectra." << endl;
			Timer sendTime(true);
		#endif

		stringstream compressedStream;
		boost::iostreams::filtering_ostream compressorStream;
		compressorStream.push( boost::iostreams::zlib_compressor() );
		compressorStream.push( compressedStream );
		boost::iostreams::copy( packStream, compressorStream );
		compressorStream.reset();

		string pack = compressedStream.str();
		int len = (int) pack.length();
		MPI_Send( &len,					1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD );
		MPI_Send( (void*) pack.c_str(),	len,	MPI_CHAR,	0,	0x01, MPI_COMM_WORLD );

		#ifdef MPI_DEBUG
			cout << g_hostString << " finished sending " << numSpectra << " prepared spectra; " <<
					sendTime.End() << " seconds elapsed." << endl;
		#endif

		return 0;
	}

	int ReceiveUnpreparedSpectraBatchFromRootProcess()
	{
		int batchSize;

		MPI_Ssend( &g_pid,		1,	MPI_INT,		0,	0xFF, MPI_COMM_WORLD );
		string pack;
		int len;

		#ifdef MPI_DEBUG
			cout << g_hostString << " is receiving a batch of unsequenced spectra." << endl;
			Timer receiveTime(true);
		#endif
		MPI_Recv( &len,					1,			MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );
		pack.resize( len );
		MPI_Recv( (void*) pack.data(),	len,		MPI_CHAR,	0,	0x01, MPI_COMM_WORLD, &st );

		stringstream compressedStream( pack );
		stringstream packStream;
		boost::iostreams::filtering_ostream decompressorStream;
		decompressorStream.push( boost::iostreams::zlib_decompressor() );
		decompressorStream.push( packStream );
		boost::iostreams::copy( compressedStream, decompressorStream );
		decompressorStream.reset();

		binary_iarchive packArchive( packStream );

		try
		{
			packArchive & batchSize;

			if( !batchSize )
			{
				#ifdef MPI_DEBUG
					cout << g_hostString << " is informed that all spectra have been sequence tagged." << endl;
				#endif

				return 0; // do not expect another batch
			}

			#ifdef MPI_DEBUG
				cout << g_hostString << " finished receiving a batch of " << batchSize << " unsequenced spectra; " <<
						receiveTime.End() << " seconds elapsed." << endl;
			#endif

			for( int j=0; j < batchSize; ++j )
			{
				Spectrum* s = new Spectrum;
				packArchive & *s;
				spectra.push_back( s );

			}
		} catch( exception& e )
		{
			cerr << g_hostString << " had an error: " << e.what() << endl;
			exit(1);
		}

		return 1; // expect another batch
	}

	int TransmitUnpreparedSpectraToChildProcesses()
	{
		int numSpectra = (int) spectra.size();

		int sourceProcess, batchSize;
		bool IsFinished = false;

		Timer PrepareTime( true );
		float totalPrepareTime = 0.01f;
		float lastUpdate = 0.0f;

		int i = 0;
		int numChildrenFinished = 0;
		while( numChildrenFinished < g_numChildren )
		{
			stringstream packStream;
			binary_oarchive packArchive( packStream );
			// For every batch, listen for a worker process that is ready to receive it

			#ifdef MPI_DEBUG
				cout << g_hostString << " is listening for a child process to offer to prepare some spectra." << endl;
			#endif

			if( i < numSpectra )
			{
				batchSize = min( numSpectra-i, g_rtConfig->SpectraBatchSize );

				try
				{
					packArchive & batchSize;

					SpectraList::iterator sItr = spectra.begin();
					advance( sItr, i );
					for( int j = i; j < i + batchSize; ++j, ++sItr )
					{
						packArchive & **sItr;
					}
				} catch( exception& e )
				{
					cerr << g_hostString << " had an error: " << e.what() << endl;
					exit(1);
				}

				i += batchSize;
			} else
			{
				batchSize = 0;
				packArchive & batchSize;

				#ifdef MPI_DEBUG
					cout << "Process #" << sourceProcess << " has been informed that preparation is complete." << endl;
				#endif

				++numChildrenFinished;
			}

			MPI_Recv( &sourceProcess,		1,		MPI_INT,	MPI_ANY_SOURCE,	0xFF, MPI_COMM_WORLD, &st );

			stringstream compressedStream;
			boost::iostreams::filtering_ostream compressorStream;
			compressorStream.push( boost::iostreams::zlib_compressor() );
			compressorStream.push( compressedStream );
			boost::iostreams::copy( packStream, compressorStream );
			compressorStream.reset();

			string pack = compressedStream.str();
			int len = (int) pack.length();

			MPI_Send( &len,					1,		MPI_INT,	sourceProcess,	0x00, MPI_COMM_WORLD );
			MPI_Send( (void*) pack.c_str(),	len,	MPI_CHAR,	sourceProcess,	0x01, MPI_COMM_WORLD );

			totalPrepareTime = PrepareTime.TimeElapsed();
			if( !IsFinished && ( ( totalPrepareTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || i == numSpectra ) )
			{
				if( i == numSpectra )
					IsFinished = true;

				float spectraPerSec = float(i) / totalPrepareTime;
				float estimatedTimeRemaining = float(numSpectra-i) / spectraPerSec;
				cout << g_hostString << " has prepared " << i << " of " << numSpectra << " spectra; " << spectraPerSec <<
						" per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
				lastUpdate = totalPrepareTime;
			}
		}

		return 0;
	}

	int ReceiveSpectraFromRootProcess()
	{
		int numSpectra;

		#ifdef MPI_DEBUG
			cout << g_hostString << " is receiving " << numSpectra << " unprepared spectra." << endl;
			Timer receiveTime(true);
		#endif

		string pack;
		int len;

		MPI_Recv( &len,					1,			MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );
		pack.resize( len );
		MPI_Recv( (void*) pack.data(),	len,		MPI_CHAR,	0,	0x01, MPI_COMM_WORLD, &st );

		stringstream compressedStream( pack );
		stringstream packStream;
		boost::iostreams::filtering_ostream decompressorStream;
		decompressorStream.push( boost::iostreams::zlib_decompressor() );
		decompressorStream.push( packStream );
		boost::iostreams::copy( compressedStream, decompressorStream );
		decompressorStream.reset();

		binary_iarchive packArchive( packStream );

		try
		{
			//cout << g_hostString << " is unpacking spectra." << endl;
			packArchive & numSpectra;
			//cout << g_hostString << " has " << numSpectra << " spectra." << endl;

			for( int j=0; j < numSpectra; ++j )
			{
				Spectrum* s = new Spectrum;
				packArchive & *s;
				spectra.push_back( s );

			}
			//cout << g_hostString << " is finished unpacking spectra." << endl;
		} catch( exception& e )
		{
			cerr << g_hostString << " had an error: " << e.what() << endl;
			exit(1);
		}

		#ifdef MPI_DEBUG
			cout << g_hostString << " finished receiving " << numSpectra << " unprepared spectra; " <<
					receiveTime.End() << " seconds elapsed." << endl;
		#endif

		return 1;
	}

	int TransmitSpectraToChildProcesses()
	{
		spectra.sort( spectraSortByID() );

		int numSpectra = (int) spectra.size();

		stringstream packStream;
		binary_oarchive packArchive( packStream );

		packArchive & numSpectra;
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
		{
			Spectrum* s = (*sItr);
			packArchive & *s;
		}

		stringstream compressedStream;
		boost::iostreams::filtering_ostream compressorStream;
		compressorStream.push( boost::iostreams::zlib_compressor() );
		compressorStream.push( compressedStream );
		boost::iostreams::copy( packStream, compressorStream );
		compressorStream.reset();

		string pack = compressedStream.str();
		int len = (int) pack.length();

		cout << g_hostString << ": " << numSpectra << " spectra packed into " << len << " bytes." << endl;

		Timer PrepareTime( true );
		float totalPrepareTime = 0.01f;
		float lastUpdate = 0.0f;
		for( int p=0; p < g_numChildren; ++p )
		{
			#ifdef MPI_DEBUG
				cout << g_hostString << " is sending " << numSpectra << " prepared spectra." << endl;
				Timer sendTime(true);
			#endif

			MPI_Send( &len,					1,		MPI_INT,	p+1,	0x00, MPI_COMM_WORLD );
			MPI_Send( (void*) pack.c_str(),	len,	MPI_CHAR,	p+1,	0x01, MPI_COMM_WORLD );

			#ifdef MPI_DEBUG
				cout << g_hostString << " finished sending " << numSpectra << " prepared spectra; " <<
						sendTime.End() << " seconds elapsed." << endl;
			#endif

			totalPrepareTime = PrepareTime.TimeElapsed();
			if( ( totalPrepareTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || p+1 == g_numChildren )
			{
				float nodesPerSec = float(p+1) / totalPrepareTime;
				float estimatedTimeRemaining = float(g_numChildren-p-1) / nodesPerSec;
				cout << g_hostString << " has sent spectra to " << p+1 << " of " << g_numChildren << " worker nodes; " << nodesPerSec <<
						" per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
				lastUpdate = totalPrepareTime;
			}
		}

		return numSpectra;
	}

	int TransmitProteinsToChildProcesses()
	{
		int numProteins = (int) proteins.size();

		vector< simplethread_handle_t > workerHandles;

		int sourceProcess, batchSize, numChildQueries;
		int numQueries = 0;
		bool IsFinished = false;

		Timer searchTime( true );
		float totalSearchTime = 0.01f;
		float lastUpdate = 0.0f;

		int i = 0;
		int numChildrenFinished = 0;
		while( numChildrenFinished < g_numChildren )
		{
			int pOffset = i;
			batchSize = min( numProteins-i, g_rtConfig->ProteinBatchSize );

			stringstream packStream;
			binary_oarchive packArchive( packStream );

			try
			{
				packArchive & pOffset;

				for( int j = i; j < i + batchSize; ++j )
				{
					packArchive & proteins[j];
				}
			} catch( exception& e )
			{
				cerr << g_hostString << " had an error: " << e.what() << endl;
				exit(1);
			}

			// For every batch, listen for a worker process that is ready to receive it

			#ifdef MPI_DEBUG
				cout << g_hostString << " is listening for a child process to offer to search some proteins." << endl;
			#endif

			MPI_Recv( &sourceProcess,			1,		MPI_INT,	MPI_ANY_SOURCE,	0xFF, MPI_COMM_WORLD, &st );
			MPI_Recv( &numChildQueries,			1,		MPI_INT,	sourceProcess,	0xEE, MPI_COMM_WORLD, &st );
			numQueries += numChildQueries;

			if( i < numProteins )
			{
				MPI_Ssend( &batchSize,			1,		MPI_INT,	sourceProcess,	0x99, MPI_COMM_WORLD );

				#ifdef MPI_DEBUG
					cout << g_hostString << " is sending " << batchSize << " proteins." << endl;
					Timer sendTime(true);
				#endif

				string pack = packStream.str();
				int len = (int) pack.length();

				MPI_Send( &len,					1,		MPI_INT,	sourceProcess,	0x00, MPI_COMM_WORLD );
				MPI_Send( (void*) pack.c_str(),	len,	MPI_CHAR,	sourceProcess,	0x01, MPI_COMM_WORLD );

				#ifdef MPI_DEBUG
					cout << g_hostString << " finished sending " << batchSize << " proteins; " <<
							sendTime.End() << " seconds elapsed." << endl;
				#endif

				i += batchSize;
			} else
			{
				batchSize = 0;
				MPI_Ssend( &batchSize,	1,	MPI_INT,	sourceProcess,	0x99, MPI_COMM_WORLD );

				#ifdef MPI_DEBUG
					cout << "Process #" << sourceProcess << " has been informed that all proteins have been searched." << endl;
				#endif

				++numChildrenFinished;
			}

			totalSearchTime = searchTime.TimeElapsed();
			if( !IsFinished && ( ( totalSearchTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || i+1 == numProteins ) )
			{
				if( i+1 == numProteins )
					IsFinished = true;

				float proteinsPerSec = float(i+1) / totalSearchTime;
				float estimatedTimeRemaining = float(numProteins-i) / proteinsPerSec;

				cout << g_hostString << " has searched " << i << " of " <<	numProteins << " proteins; " << proteinsPerSec <<
						" per second, " << totalSearchTime << " elapsed, " << estimatedTimeRemaining << " remaining." << endl;
					
				//cout << threadInfo->workerHostString << " has searched " << curProtein << " of " <<	threadInfo->endIndex+1 <<
				//		" proteins " << i+1 << "; " << proteinsPerSec << " per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
				//float candidatesPerSec = numQueries / totalSearchTime;
				//estimatedTimeRemaining = float( numCandidates - numQueries ) / candidatesPerSec;
				//cout << g_hostString << " has made " << numQueries << " of about " << numCandidates << " comparisons; " <<
				//		candidatesPerSec << " per second, " << estimatedTimeRemaining << " seconds remaining." << endl;

				lastUpdate = totalSearchTime;
			}
		}

		return 0;
	}

	int ReceiveProteinBatchFromRootProcess( int lastQueryCount )
	{
		int batchSize;

		MPI_Ssend( &g_pid,			1,				MPI_INT,	0,	0xFF, MPI_COMM_WORLD );
		MPI_Ssend( &lastQueryCount,	1,				MPI_INT,	0,	0xEE, MPI_COMM_WORLD );

		MPI_Recv( &batchSize,		1,				MPI_INT,	0,	0x99, MPI_COMM_WORLD, &st );

		if( !batchSize )
		{
			#ifdef MPI_DEBUG
				cout << g_hostString << " is informed that all proteins have been searched." << endl;
			#endif

			return 0; // expect another batch
		}

		#ifdef MPI_DEBUG
			cout << g_hostString << " is receiving " << batchSize << " proteins." << endl;
			Timer receiveTime(true);
		#endif

		string pack;
		int len;

		MPI_Recv( &len,					1,			MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );
		pack.resize( len );
		MPI_Recv( (void*) pack.data(),	len,		MPI_CHAR,	0,	0x01, MPI_COMM_WORLD, &st );

		#ifdef MPI_DEBUG
			cout << g_hostString << " finished receiving " << batchSize << " proteins; " <<
					receiveTime.End() << " seconds elapsed." << endl;
		#endif

		stringstream packStream( pack );
		binary_iarchive packArchive( packStream );

		try
		{
			packArchive & g_rtConfig->ProteinIndexOffset;

			for( int i=0; i < batchSize; ++i )
			{
				proteins.push_back( proteinData() );
				proteinData& p = proteins.back();
				packArchive & p;
			}
		} catch( exception& e )
		{
			cerr << g_hostString << " had an error: " << typeid(e).name() << " (" << e.what() << ")" << endl;
			exit(1);
		}

		return 1; // don't expect another batch
	}

	int TransmitResultsToRootProcess( const searchStats& stats )
	{
		int numSpectra = (int) spectra.size();

		stringstream packStream;
		binary_oarchive packArchive( packStream );

		packArchive & numSpectra;
		packArchive & stats;
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
		{
			Spectrum* s = (*sItr);
			s->peakData.clear();
			packArchive & *s;
		}

		stringstream compressedStream;
		boost::iostreams::filtering_ostream compressorStream;
		compressorStream.push( boost::iostreams::zlib_compressor() );
		compressorStream.push( compressedStream );
		boost::iostreams::copy( packStream, compressorStream );
		compressorStream.reset();

		string pack = compressedStream.str();
		int len = (int) pack.length();

		//cout << g_hostString << ": " << numSpectra << " results packed into " << len << " bytes." << endl;

		#ifdef MPI_DEBUG
			cout << g_hostString << " is sending " << numSpectra << " search results." << endl;
			Timer sendTime(true);
		#endif

		MPI_Ssend( &g_pid,				1,		MPI_INT,	0,	0xEE, MPI_COMM_WORLD );
		MPI_Send( &len,					1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD );
		MPI_Send( (void*) pack.c_str(),	len,	MPI_CHAR,	0,	0x01, MPI_COMM_WORLD );


		#ifdef MPI_DEBUG
			cout << g_hostString << " finished sending search results; " <<
					sendTime.End() << " seconds elapsed." << endl;
		#endif

		return 0;
	}

	int ReceiveResultsFromChildProcesses(searchStats& overallSearchStats)
	{
		int numSpectra;
		int sourceProcess;

		Timer ResultsTime( true );
		float totalResultsTime = 0.01f;
		float lastUpdate = 0.0f;

		for( int p=0; p < g_numChildren; ++p )
		{
			MPI_Recv( &sourceProcess,		1,		MPI_INT,	MPI_ANY_SOURCE,	0xEE, MPI_COMM_WORLD, &st );

			#ifdef MPI_DEBUG
				cout << g_hostString << " is receiving search results." << endl;
				Timer receiveTime(true);
			#endif

			string pack;
			int len;

			MPI_Recv( &len,					1,		MPI_INT,	sourceProcess,	0x00, MPI_COMM_WORLD, &st );
			pack.resize( len );
			MPI_Recv( (void*) pack.data(),	len,	MPI_CHAR,	sourceProcess,	0x01, MPI_COMM_WORLD, &st );

			stringstream compressedStream( pack );
			stringstream packStream;
			boost::iostreams::filtering_ostream decompressorStream;
			decompressorStream.push( boost::iostreams::zlib_decompressor() );
			decompressorStream.push( packStream );
			boost::iostreams::copy( compressedStream, decompressorStream );
			decompressorStream.reset();

			binary_iarchive packArchive( packStream );

			try
			{
				searchStats childSearchStats;
				packArchive & numSpectra;
				packArchive & childSearchStats;
				overallSearchStats = overallSearchStats + childSearchStats;

				//cout << g_hostString << " is unpacking results for " << numSpectra << " spectra." << endl;
				for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
				{
					Spectrum* childSpectrum = new Spectrum;
					Spectrum* rootSpectrum = *sItr;
					packArchive & *childSpectrum;
					rootSpectrum->numTargetComparisons += childSpectrum->numTargetComparisons;
                    rootSpectrum->numDecoyComparisons += childSpectrum->numDecoyComparisons;
					rootSpectrum->processingTime += childSpectrum->processingTime;
					for( Spectrum::SearchResultSetType::iterator itr = childSpectrum->resultSet.begin(); itr != childSpectrum->resultSet.end(); ++itr )
						rootSpectrum->resultSet.add( *itr );
					rootSpectrum->scoreHistogram += childSpectrum->scoreHistogram;
					delete childSpectrum;
				}
				//cout << g_hostString << " is finished unpacking results." << endl;
			} catch( exception& e )
			{
				cerr << g_hostString << " had an error: " << e.what() << endl;
				exit(1);
			}

			#ifdef MPI_DEBUG
				cout << g_hostString << " finished receiving " << numSpectra << " search results; " <<
						receiveTime.End() << " seconds elapsed.";
			#endif

			totalResultsTime = ResultsTime.TimeElapsed();
			if( ( totalResultsTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || p+1 == g_numChildren )
			{
				float nodesPerSec = float(p+1) / totalResultsTime;
				float estimatedTimeRemaining = float(g_numChildren-p-1) / nodesPerSec;
				cout << g_hostString << " has received results from " << p+1 << " of " << g_numChildren << " worker nodes; " << nodesPerSec <<
						" per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
				lastUpdate = totalResultsTime;
			}

		}

		return 0;
	}
}
}
#endif
