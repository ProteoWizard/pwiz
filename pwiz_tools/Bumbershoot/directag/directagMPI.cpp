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
// Contributor(s): Surendra Dasari
//

#include "stdafx.h"
#include "directag.h"

#ifdef USE_MPI

#include <boost/iostreams/filtering_stream.hpp>
#include <boost/iostreams/copy.hpp>
#include <boost/iostreams/filter/zlib.hpp>
#include <boost/serialization/variant.hpp>

BOOST_CLASS_IMPLEMENTATION(boost::atomic_uint32_t, boost::serialization::primitive_type)
BOOST_CLASS_IMPLEMENTATION(boost::atomic_uint64_t, boost::serialization::primitive_type)

namespace freicore
{
    
MPI_Status		st;
void*			g_mpiBuffer;

namespace directag
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

		MPI_Recv( &len,								1,		MPI_INT,			0,		0x00, MPI_COMM_WORLD, &st );
		g_rtConfig->cfgStr.resize( len );
		MPI_Recv( &g_rtConfig->cfgStr[0],			len,	MPI_CHAR,			0,		0x01, MPI_COMM_WORLD, &st );
		g_rtConfig->initializeFromBuffer( g_rtConfig->cfgStr, "\r\n#" );

		g_residueMap = new ResidueMap();
		MPI_Recv( &len,								1,		MPI_INT,			0,		0x02, MPI_COMM_WORLD, &st );
		g_residueMap->cfgStr.resize( len );
		MPI_Recv( &g_residueMap->cfgStr[0],			len,	MPI_CHAR,			0,		0x03, MPI_COMM_WORLD, &st );
		g_residueMap->initializeFromBuffer( g_residueMap->cfgStr );
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
						//packArchive & (*sItr)->resultSet;
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

			string pack = packStream.str();
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

	int ReceiveUnpreparedSpectraBatchFromRootProcess()
	{
		int batchSize;

		MPI_Ssend( &g_pid,		1,	MPI_INT,		0,	0xFF, MPI_COMM_WORLD );
		string pack;
		int len;

		#ifdef MPI_DEBUG
			cout << g_hostString << " is receiving a batch of unprepared spectra." << endl;
			Timer receiveTime(true);
		#endif
		MPI_Recv( &len,					1,			MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );
		pack.resize( len );
		MPI_Recv( (void*) pack.data(),	len,		MPI_CHAR,	0,	0x01, MPI_COMM_WORLD, &st );

		stringstream packStream( pack );
		binary_iarchive packArchive( packStream );

		try
		{
			packArchive & batchSize;

			if( !batchSize )
			{
				#ifdef MPI_DEBUG
					cout << g_hostString << " is informed that all spectra have been prepared." << endl;
				#endif

				return 0; // do not expect another batch
			}

			#ifdef MPI_DEBUG
				cout << g_hostString << " finished receiving a batch of " << batchSize << " unprepared spectra; " <<
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

	int TransmitUntaggedSpectraToChildProcesses()
	{
		int numSpectra = (int) spectra.size();

		int sourceProcess, batchSize;
		bool IsFinished = false;

		Timer taggingTime( true );
		float totalTaggingTime = 0.01f;
		float lastUpdate = 0.0f;

		int i = 0;
		int numChildrenFinished = 0;

		while( numChildrenFinished < g_numChildren )
		{
			stringstream packStream;
			binary_oarchive packArchive( packStream );

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
						delete *sItr;
					}
					//spectra.erase( sItr );
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
					cout << "Process #" << sourceProcess << " has been informed that tagging is complete." << endl;
				#endif

				++numChildrenFinished;
			}

			// For every batch, listen for a worker process that is ready to receive it

			#ifdef MPI_DEBUG
				cout << g_hostString << " is listening for a child process to offer to sequence tag some spectra." << endl;
			#endif

			MPI_Recv( &sourceProcess,			1,		MPI_INT,	MPI_ANY_SOURCE,	0xFF, MPI_COMM_WORLD, &st );

			#ifdef MPI_DEBUG
				cout << g_hostString << " is sending a batch of " << batchSize << " unsequenced spectra." << endl;
						Timer sendTime(true);
			#endif

			string pack = packStream.str();
			int len = (int) pack.length();
			MPI_Send( &len,						1,		MPI_INT,	sourceProcess,	0x00, MPI_COMM_WORLD );
			MPI_Send( (void*) pack.c_str(),		len,	MPI_CHAR,	sourceProcess,	0x01, MPI_COMM_WORLD );

			#ifdef MPI_DEBUG
				cout << g_hostString << " finished sending a batch of spectra; " <<
						sendTime.End() << " seconds elapsed." << endl;
			#endif

			totalTaggingTime = taggingTime.TimeElapsed();
			if( !IsFinished && ( ( totalTaggingTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || i+1 == numSpectra ) )
			{
				if( i+1 == numSpectra )
					IsFinished = true;

				float spectraPerSec = float(i+1) / totalTaggingTime;
				bpt::time_duration estimatedTimeRemaining(0, 0, round((numSpectra - i) / spectraPerSec));

		        cout << "Sequence tagged " << i << " of " << numSpectra << " spectra; "
                     << round(spectraPerSec) << " per second, "
                     << format_date_time("%H:%M:%S", bpt::time_duration(0, 0, round(totalTaggingTime))) << " elapsed, "
                     << format_date_time("%H:%M:%S", estimatedTimeRemaining) << " remaining." << endl;

				lastUpdate = totalTaggingTime;
			}
		}
		spectra.clear(false);
		return 0;
	}

	int ReceiveUntaggedSpectraBatchFromRootProcess()
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

		stringstream packStream( pack );
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

	int ReceiveTaggedSpectraFromChildProcesses()
	{
		int sourceProcess, numSpectra;
		for( int p=0; p < g_numChildren; ++p )
		{
			MPI_Recv( &sourceProcess,					1,	MPI_INT,	MPI_ANY_SOURCE,	0xEE, MPI_COMM_WORLD, &st );

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
                TaggingStatistics childTaggingStatistics;
				packArchive & numSpectra;
                packArchive & childTaggingStatistics;
				taggingStatistics = taggingStatistics + childTaggingStatistics;

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
		}

		return 0;
	}

	int TransmitTaggedSpectraToRootProcess( SpectraList& preparedSpectra )
	{
		int numSpectra = (int) preparedSpectra.size();

		stringstream packStream;
		binary_oarchive packArchive( packStream );

		//Timer packTime(true);
		//cout << g_hostString << " is packing " << numSpectra << " results." << endl;
		packArchive & numSpectra;
        packArchive & taggingStatistics;
		for( SpectraList::iterator sItr = preparedSpectra.begin(); sItr != preparedSpectra.end(); ++sItr )
		{
			Spectrum* s = (*sItr);

			packArchive & *s;
		}
		//cout << g_hostString << " finished packing results; " << packTime.End() << " seconds elapsed." << endl;

		MPI_Ssend( &g_pid,			1,				MPI_INT,	0,	0xEE, MPI_COMM_WORLD );

	#ifdef MPI_DEBUG
			cout << g_hostString << " is sending " << numSpectra << " packed results." << endl;
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
			cout << g_hostString << " finished sending " << numSpectra << " packed results; " <<
					sendTime.End() << " seconds elapsed." << endl;
	#endif

		return 0;
	}

} // namespace directag
} // namespace freicore

#endif
