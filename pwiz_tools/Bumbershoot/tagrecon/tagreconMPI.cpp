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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#define BOOST_LIB_DIAGNOSTIC
#include "stdafx.h"

//#define MPI_DEBUG 1

#ifdef USE_MPI

#include "tagrecon.h"
#include <boost/iostreams/filtering_stream.hpp>
#include <boost/iostreams/copy.hpp>
#include <boost/iostreams/filter/zlib.hpp>
#include <pwiz/data/proteome/Serializer_FASTA.hpp>
#include <boost/serialization/variant.hpp>

BOOST_CLASS_IMPLEMENTATION(boost::atomic_uint32_t, boost::serialization::primitive_type)
BOOST_CLASS_IMPLEMENTATION(boost::atomic_uint64_t, boost::serialization::primitive_type)

namespace freicore
{
    MPI_Status        st;
    void*            g_mpiBuffer;
namespace tagrecon
{
    /**!
        TransmitConfigsToChildProcesses transmits the configuration parameters and residue map
        to all the child process.
    */
    void TransmitConfigsToChildProcesses()
    {
        for( int p=0; p < g_numChildren; ++p )
        {
            int len;

            // Send the length and the contents of the program parameters to all the child
            // processes.
            len = (int) g_rtConfig->cfgStr.length();
            MPI_Send( &len,                                    1,        MPI_INT,            p+1,    0x00, MPI_COMM_WORLD );
            MPI_Send( (void*) g_rtConfig->cfgStr.c_str(),    len,    MPI_CHAR,            p+1,    0x01, MPI_COMM_WORLD );
        }
    }

    
    /**!
        ReceiveConfigsFromRootProcess receives the program parameters and residue map from the
        root process.
    */
    void ReceiveConfigsFromRootProcess()
    {
        int len;

        // First get the program configuration parameters and parse them out.
        MPI_Recv( &len,                                        1,        MPI_INT,            0,        0x00, MPI_COMM_WORLD, &st );
        g_rtConfig->cfgStr.resize( len );
        MPI_Recv( &g_rtConfig->cfgStr[0],                    len,    MPI_CHAR,            0,        0x01, MPI_COMM_WORLD, &st );
        g_rtConfig->initializeFromBuffer( g_rtConfig->cfgStr );
    }

    void TransmitNETRewardsToChildProcess()
    {
        for( int p=0; p < g_numChildren; ++p )
        {
            
            // Create a binary archive
            stringstream packStream;
            binary_oarchive packArchive( packStream );
            packArchive & g_rtConfig->NETRewardVector;
        
            int len = (int) packStream.str().length();
            MPI_Send( &len,                                    1,        MPI_INT,            p+1,        0x00, MPI_COMM_WORLD );
            MPI_Send( (void*) packStream.str().c_str(),        len,    MPI_CHAR,            p+1,        0x01, MPI_COMM_WORLD );
        }
    }

    void ReceiveNETRewardsFromRootProcess()
    {
        string pack;
        int len;

        // Get the binary compressed NET probability data from the root.
        MPI_Recv( &len,                    1,            MPI_INT,    0,    0x00, MPI_COMM_WORLD, &st );
        pack.resize( len );
        MPI_Recv( (void*) pack.data(),    len,        MPI_CHAR,    0,    0x01, MPI_COMM_WORLD, &st );

        stringstream packStream;
        packStream << pack;
        binary_iarchive packArchive( packStream );
        packArchive & g_rtConfig->NETRewardVector;
    }

    /**!
        ReceivePreparedSpectraFromChildProcesses receives the prepared spectra from all the child
        processes. The retreival happens in a non-deterministic manner (i.e which ever process is
        ready to transmit the processed spectra will transmit them and the root will retreive them
        on a first-come-first-served basis).
    */
    int ReceivePreparedSpectraFromChildProcesses()
    {
        Timer receiveTime( true );
        float totalReceiveTime = 0.01f;
        float lastUpdate = 0.0f;
        int sourceProcess, numSpectra;
        // For each process.
        for( int p=0; p < g_numChildren; ++p )
        {
            // Get the source process that is sending the spectra packet
            MPI_Recv( &sourceProcess,        1,    MPI_INT,    MPI_ANY_SOURCE,    0xEE, MPI_COMM_WORLD, &st );

            #ifdef MPI_DEBUG
                cout << g_hostString << " is receiving " << numSpectra << " prepared spectra." << endl;
                Timer receiveTime(true);
            #endif

            string pack;
            int len;

            // Get the length and the contents of the data packet.
            MPI_Recv( &len,                    1,            MPI_INT,    sourceProcess,    0x00, MPI_COMM_WORLD, &st );
            pack.resize( len );
            MPI_Recv( (void*) pack.data(),    len,        MPI_CHAR,    sourceProcess,    0x01, MPI_COMM_WORLD, &st );

            // Parse out the data packet. The data packet comes as a binary compressed
            // character stream.
            stringstream compressedStream( pack );
            stringstream packStream;
            boost::iostreams::filtering_ostream decompressorStream;
            decompressorStream.push( boost::iostreams::zlib_decompressor() );
            decompressorStream.push( packStream );
            boost::iostreams::copy( compressedStream, decompressorStream );
            decompressorStream.reset();

            binary_iarchive packArchive( packStream );

            // Recieve the spectrum as a pointer to the spectrum
            // object
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

            // Some stats
            totalReceiveTime = receiveTime.TimeElapsed();
            if( ( totalReceiveTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || p+1 == g_numChildren )
            {
                float nodesPerSec = float(p+1) / totalReceiveTime;
                float estimatedTimeRemaining = float(g_numChildren-p-1) / nodesPerSec;
                cout << "Received prepared spectra from " << p+1 << " of " << g_numChildren << " worker nodes; " << nodesPerSec <<
                        " per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
                lastUpdate = totalReceiveTime;
            }
        }
        return 0;
    }

    /**!
        TransmitPreparedSpectraToRootProcess sends the prepared spectra to the root process using
        message-passing interface (MPI).
    */
    int TransmitPreparedSpectraToRootProcess( SpectraList& preparedSpectra )
    {
        // Get the number of spectra
        int numSpectra = (int) preparedSpectra.size();

        // Create a binary archive
        stringstream packStream;
        binary_oarchive packArchive( packStream );

        // Pack the spectrum object as a reference to the spectrum. The packing
        // is a binary stream with a pointer to the number of spectra variable 
        // followed by the pointers to the actual spectrum objects.
        //Timer packTime(true);
        //cout << g_hostString << " is packing " << numSpectra << " results." << endl;
        packArchive & numSpectra;
        for( SpectraList::iterator sItr = preparedSpectra.begin(); sItr != preparedSpectra.end(); ++sItr )
        {
            Spectrum* s = *sItr;
            packArchive & *s;
        }
        //cout << g_hostString << " finished packing results; " << packTime.End() << " seconds elapsed." << endl;

        // Send the process ID that is sending the spectra
        MPI_Ssend( &g_pid,        1,    MPI_INT,        0,    0xEE, MPI_COMM_WORLD );

        #ifdef MPI_DEBUG
            cout << g_hostString << " is sending " << numSpectra << " prepared spectra." << endl;
            Timer sendTime(true);
        #endif

        // Pack the pointer stream into a bianry compressed string
        stringstream compressedStream;
        boost::iostreams::filtering_ostream compressorStream;
        compressorStream.push( boost::iostreams::zlib_compressor() );
        compressorStream.push( compressedStream );
        boost::iostreams::copy( packStream, compressorStream );
        compressorStream.reset();

        // Send the packed stream
        string pack = compressedStream.str();
        int len = (int) pack.length();
        MPI_Send( &len,                    1,        MPI_INT,    0,    0x00, MPI_COMM_WORLD );
        MPI_Send( (void*) pack.c_str(),    len,    MPI_CHAR,    0,    0x01, MPI_COMM_WORLD );

        #ifdef MPI_DEBUG
            cout << g_hostString << " finished sending " << numSpectra << " prepared spectra; " <<
                    sendTime.End() << " seconds elapsed." << endl;
        #endif

        return 0;
    }

    /**!
        ReceiveUnpreparedSpectraBatchFromRootProcess receives unprepared spectra from the root
        process using message-passing interface (MPI)
    */
    int ReceiveUnpreparedSpectraBatchFromRootProcess()
    {
        int batchSize;

        // Send the process id for the child process to the root.
        MPI_Ssend( &g_pid,        1,    MPI_INT,        0,    0xFF, MPI_COMM_WORLD );
        string pack;
        int len;

        #ifdef MPI_DEBUG
            cout << g_hostString << " is receiving a batch of unsequenced spectra." << endl;
            Timer receiveTime(true);
        #endif
        // Get the binary compressed spectral data from the root.
        MPI_Recv( &len,                    1,            MPI_INT,    0,    0x00, MPI_COMM_WORLD, &st );
        pack.resize( len );
        MPI_Recv( (void*) pack.data(),    len,        MPI_CHAR,    0,    0x01, MPI_COMM_WORLD, &st );

        // Unpack the data
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
            // Get the data and check the size of the spectra in the data packet
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

            // Retreive the spectrum pointers
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

    /**!
        TransmitUnpreparedSpectraToChildProcesses transmits the unprepared spectra to all the
        child processes using message-passing interface (MPI). This function sends the spectra
        to the child process in batches. 
    */
    int TransmitUnpreparedSpectraToChildProcesses()
    {
        // Get a count of the total number of spectra remaining.
        int numSpectra = (int) spectra.size();

        int sourceProcess, batchSize;
        bool IsFinished = false;

        Timer PrepareTime( true );
        float totalPrepareTime = 0.01f;
        float lastUpdate = 0.0f;
        
        //int totalSpectrumSent = 0;

        int i = 0;
        int numChildrenFinished = 0;
        // If there are still some processes alive
        while( numChildrenFinished < g_numChildren )
        {
            // Get a binary stream
            stringstream packStream;
            binary_oarchive packArchive( packStream );
            // For every batch, listen for a worker process that is ready to receive it

            #ifdef MPI_DEBUG
                cout << g_hostString << " is listening for a child process to offer to prepare some spectra." << endl;
            #endif

            // If there are still some spectra remaining to be processed
            if( i < numSpectra )
            {
                // Compute the batch size depending on the number of spectra left and the
                // maximum batch size
                batchSize = min( numSpectra-i, g_rtConfig->SpectraBatchSize );

                try
                {
                    // Go the pointer that points the spectrum object
                    // starting at the begining of the batch
                    packArchive & batchSize;

                    SpectraList::iterator sItr = spectra.begin();
                    advance( sItr, i );
                    // Get the pointer to the spectrum object
                    for( int j = i; j < i + batchSize; ++j, ++sItr ) {
                        packArchive & **sItr;
                        //totalSpectrumSent++;
                    }
                } catch( exception& e )
                {
                    cerr << g_hostString << " had an error: " << e.what() << endl;
                    exit(1);
                }

                i += batchSize;
            } else
            {
                // If the all the spectra have been processed then set the batch size to 0.
                batchSize = 0;
                packArchive & batchSize;

                #ifdef MPI_DEBUG
                    cout << "Process #" << sourceProcess << " has been informed that preparation is complete." << endl;
                #endif

                ++numChildrenFinished;
            }

            // Get the process ID for the process that sent a request for the spectra
            MPI_Recv( &sourceProcess,        1,        MPI_INT,    MPI_ANY_SOURCE,    0xFF, MPI_COMM_WORLD, &st );

            // Pack the data stream
            stringstream compressedStream;
            boost::iostreams::filtering_ostream compressorStream;
            compressorStream.push( boost::iostreams::zlib_compressor() );
            compressorStream.push( compressedStream );
            boost::iostreams::copy( packStream, compressorStream );
            compressorStream.reset();

            string pack = compressedStream.str();
            int len = (int) pack.length();

            // Send the spectra to the source process that requested them
            MPI_Send( &len,                    1,        MPI_INT,    sourceProcess,    0x00, MPI_COMM_WORLD );
            MPI_Send( (void*) pack.c_str(),    len,    MPI_CHAR,    sourceProcess,    0x01, MPI_COMM_WORLD );

            // Compute some stats
            totalPrepareTime = PrepareTime.TimeElapsed();
            if( !IsFinished && ( ( totalPrepareTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || i == numSpectra ) )
            {
                if( i == numSpectra )
                    IsFinished = true;

                float spectraPerSec = float(i) / totalPrepareTime;
                float estimatedTimeRemaining = float(numSpectra-i) / spectraPerSec;
                cout << "Prepared " << i << " of " << numSpectra << " spectra; " << spectraPerSec <<
                        " per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
                lastUpdate = totalPrepareTime;
            }
        }

        //cout << "totalSpectrumSent:" << totalSpectrumSent << endl;
        return 0;
    }

    /**!
        ReceiveSpectraFromRootProcess function receives spectra from the root process using message-passing
        interface.
    */
    int ReceiveSpectraFromRootProcess()
    {
        int numSpectra;
        int done;

        #ifdef MPI_DEBUG
            cout << g_hostString << " is receiving " << numSpectra << " unprepared spectra." << endl;
            Timer receiveTime(true);
        #endif

        string pack;
        int len;

        // Get the length of the steam and the data in the stream itself
        MPI_Recv( &len,                    1,            MPI_INT,    0,    0x00, MPI_COMM_WORLD, &st );
        pack.resize( len );
        MPI_Recv( (void*) pack.data(),    len,        MPI_CHAR,    0,    0x01, MPI_COMM_WORLD, &st );

        // Unpack the stream
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
            // Get the total number of spectra out of the stream
            //cout << g_hostString << " is unpacking spectra." << endl;
            packArchive & numSpectra;
            //cout << g_hostString << " has " << numSpectra << " spectra." << endl;
            packArchive & done;

            // Get the pointers to the spectrum objects
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

        return done;
    }

    /**!
        TransmitSpectraToChildProcesses tramits spectra to the child processes using message-passing'
        interface. This function sends the spectra to all the child processes
    */
    int TransmitSpectraToChildProcesses(int done)
    {
        spectra.sort( spectraSortByID() );

        int numSpectra = (int) spectra.size();

        stringstream packStream;
        binary_oarchive packArchive( packStream );

        // Pack the data stream with pointers to the spectrum objects
        packArchive & numSpectra;
        packArchive & done;
        for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
        {
            Spectrum* s = (*sItr);
            packArchive & *s;
        }

        // Compress the data stream
        stringstream compressedStream;
        boost::iostreams::filtering_ostream compressorStream;
        compressorStream.push( boost::iostreams::zlib_compressor() );
        compressorStream.push( compressedStream );
        boost::iostreams::copy( packStream, compressorStream );
        compressorStream.reset();

        string pack = compressedStream.str();
        int len = (int) pack.length();

        cout << "Packed " << numSpectra << " spectra into " << len << " bytes." << endl;

        Timer PrepareTime( true );
        float totalPrepareTime = 0.01f;
        float lastUpdate = 0.0f;
        // Send the stream to all the child processes
        for( int p=0; p < g_numChildren; ++p )
        {
            #ifdef MPI_DEBUG
                cout << g_hostString << " is sending " << numSpectra << " prepared spectra." << endl;
                Timer sendTime(true);
            #endif

            MPI_Send( &len,                    1,        MPI_INT,    p+1,    0x00, MPI_COMM_WORLD );
            MPI_Send( (void*) pack.c_str(),    len,    MPI_CHAR,    p+1,    0x01, MPI_COMM_WORLD );

            #ifdef MPI_DEBUG
                cout << g_hostString << " finished sending " << numSpectra << " prepared spectra; " <<
                        sendTime.End() << " seconds elapsed." << endl;
            #endif

            totalPrepareTime = PrepareTime.TimeElapsed();
            if( ( totalPrepareTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || p+1 == g_numChildren )
            {
                float nodesPerSec = float(p+1) / totalPrepareTime;
                float estimatedTimeRemaining = float(g_numChildren-p-1) / nodesPerSec;
                cout << "Sent spectra to " << p+1 << " of " << g_numChildren << " worker nodes; " << round(nodesPerSec, 1) <<
                        " per second, " << round(estimatedTimeRemaining) << " seconds remaining." << endl;
                lastUpdate = totalPrepareTime;
            }
        }

        return numSpectra;
    }

    /**!
        TransmitProteinsToChildProcesses trasmits a chunk of proteins at a time to all the child processes using
        message-passing interface.
    */
    int TransmitProteinsToChildProcesses()
    {
        // Get the size of the protein database
        int numProteins = (int) proteins.size();

        vector< simplethread_handle_t > workerHandles;

        int sourceProcess, batchSize;
        bool IsFinished = false;

        Timer searchTime( true );
        float totalSearchTime = 0.01f;
        float lastUpdate = 0.0f;

        int i = 0;
        int numChildrenFinished = 0;
        // While there are child processes to process the data
        while( numChildrenFinished < g_numChildren )
        {
            #ifdef MPI_DEBUG
                cout << g_hostString << " is listening for a child process to offer to search some proteins." << endl;
            #endif
            // Listen for a process requesting proteins. 
            // Extract the number of CPUs available on the process.
            MPI_Recv( &sourceProcess,            1,        MPI_INT,    MPI_ANY_SOURCE,    0xFF, MPI_COMM_WORLD, &st );
            int sourceCPUs = 0;
            MPI_Recv( &sourceCPUs,                1,        MPI_INT,    sourceProcess,    0xFF, MPI_COMM_WORLD, &st );
            
            int pOffset = i;
            // Scale the batchSize with the number of cpus in the requested process.
            batchSize = min( numProteins-i, g_rtConfig->ProteinBatchSize*sourceCPUs );

            // Pack the protein batch size and the protein sequences
            // in a stream
            stringstream packStream;
            binary_oarchive packArchive( packStream );

            try
            {
                packArchive & pOffset;
                string proteinStream;
                for( int j = i; j < i + batchSize; ++j )
                {
                    proteinStream += ">" + proteins[j].getName() + " " + proteins[j].getDescription() + "\n" 
                                  + proteins[j].getSequence() + "\n";
                }
                packArchive & proteinStream;
            } catch( exception& e )
            {
                cerr << g_hostString << " had an error: " << e.what() << endl;
                exit(1);
            }
            #ifdef MPI_DEBUG
                cout << "Process #" << sourceProcess << " has " << sourceCPUs << " cpus. Sending " << batchSize << " proteins." << endl;
            #endif

            if( i < numProteins )
            {
                // Send the batch size to the child process
                MPI_Ssend( &batchSize,            1,        MPI_INT,    sourceProcess,    0x99, MPI_COMM_WORLD );

                #ifdef MPI_DEBUG
                    cout << g_hostString << " is sending " << batchSize << " proteins." << endl;
                    Timer sendTime(true);
                #endif

                // Pack the protein sequences and send them
                string pack = packStream.str();
                int len = (int) pack.length();

                MPI_Send( &len,                    1,        MPI_INT,    sourceProcess,    0x00, MPI_COMM_WORLD );
                MPI_Send( (void*) pack.c_str(),    len,    MPI_CHAR,    sourceProcess,    0x01, MPI_COMM_WORLD );

                #ifdef MPI_DEBUG
                    cout << g_hostString << " finished sending " << batchSize << " proteins; " <<
                            sendTime.End() << " seconds elapsed." << endl;
                #endif

                i += batchSize;
            } else
            {
                // If the child process finished processing the data then update the
                // processes available for processing
                batchSize = 0;
                MPI_Ssend( &batchSize,    1,    MPI_INT,    sourceProcess,    0x99, MPI_COMM_WORLD );

                #ifdef MPI_DEBUG
                    cout << "Process #" << sourceProcess << " has been informed that all proteins have been searched." << endl;
                #endif

                ++numChildrenFinished;
            }

            // Some stats
            totalSearchTime = searchTime.TimeElapsed();
            if( !IsFinished && ( ( totalSearchTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || i+1 == numProteins ) )
            {
                if( i+1 == numProteins )
                    IsFinished = true;

                float proteinsPerSec = float(i+1) / totalSearchTime;
                bpt::time_duration estimatedTimeRemaining(0, 0, round((numProteins - i) / proteinsPerSec));

                cout << "Searched " << i << " of " << numProteins << " proteins; "
                     << round(proteinsPerSec) << " per second, "
                     << format_date_time("%H:%M:%S", bpt::time_duration(0, 0, round(totalSearchTime))) << " elapsed, "
                     << format_date_time("%H:%M:%S", estimatedTimeRemaining) << " remaining." << endl;

                lastUpdate = totalSearchTime;
            }
        }

        return 0;
    }


    /**!
        ReceiveProteinBatchFromRootProcess sends out the number of queries done by a child process and
        asks for a fresh batch of proteins for searching the spectra. The function using message-passing
        interface to communicate with the root process.
    */
    int ReceiveProteinBatchFromRootProcess()
    {
        int batchSize;

        // Send the process id and the number of queries it has finished
        MPI_Ssend( &g_pid,            1,                MPI_INT,    0,    0xFF, MPI_COMM_WORLD );
        MPI_Ssend( &g_numWorkers,    1,                MPI_INT,    0,    0xFF, MPI_COMM_WORLD );

        // Get the fresh batch of the proteins from the root process
        MPI_Recv( &batchSize,        1,                MPI_INT,    0,    0x99, MPI_COMM_WORLD, &st );

        // If the batchSize is 0 then all the proteins have been processed
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

        // Otherwise you need to unpack the protein data
        string pack;
        int len;

        MPI_Recv( &len,                    1,            MPI_INT,    0,    0x00, MPI_COMM_WORLD, &st );
        pack.resize( len );
        MPI_Recv( (void*) pack.data(),    len,        MPI_CHAR,    0,    0x01, MPI_COMM_WORLD, &st );

        #ifdef MPI_DEBUG
            cout << g_hostString << " finished receiving " << batchSize << " proteins; " <<
                    receiveTime.End() << " seconds elapsed." << endl;
        #endif

        // Unpack the stream
        stringstream packStream( pack );
        binary_iarchive packArchive( packStream );

        try
        {
            // Get the index offset into the protein database
            packArchive & g_rtConfig->ProteinIndexOffset;

            string proteinString;
            packArchive & proteinString;
            shared_ptr<std::istream> proteinStream(new std::istringstream(proteinString));

            Serializer_FASTA unpacker;
            shared_ptr<ProteomeData> subsetProteinsPtr(new ProteomeData);
            unpacker.read(proteinStream, *subsetProteinsPtr);
            proteins = proteinStore(subsetProteinsPtr, g_rtConfig->DecoyPrefix, false);
        } catch( exception& e )
        {
            cerr << g_hostString << " had an error: " << typeid(e).name() << " (" << e.what() << ")" << endl;
            exit(1);
        }

        return 1; // don't expect another batch
    }

    /**!
        TransmitResultsToRootProcess clears the spectral peak data and sends out the spectra along with
        search results to the root process. The function uses message-passing interface for communicating
        with the root process
    */
    int TransmitResultsToRootProcess()
    {
        // Get the number of spectra
        int numSpectra = (int) spectra.size();

        // Clear the peak data and serialize the spectrum objects
        stringstream packStream;
        binary_oarchive packArchive( packStream );

        packArchive & numSpectra;
        packArchive & searchStatistics;
        for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
        {
            Spectrum* s = (*sItr);
            s->peakData.clear();
            packArchive & *s;
        }

        // Compress the serialized stream
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

        // Send the process id that is sending out the spectral search results
        MPI_Ssend( &g_pid,                1,        MPI_INT,    0,    0xEE, MPI_COMM_WORLD );
        // Send the data 
        MPI_Send( &len,                    1,        MPI_INT,    0,    0x00, MPI_COMM_WORLD );
        MPI_Send( (void*) pack.c_str(),    len,    MPI_CHAR,    0,    0x01, MPI_COMM_WORLD );


        #ifdef MPI_DEBUG
            cout << g_hostString << " finished sending search results; " <<
                    sendTime.End() << " seconds elapsed." << endl;
        #endif

        return 0;
    }

    /**!
        ReceiveResultsFromChildProcesses function receives the search results from all the process. The function
        receives the results on a first-come-first-served basis using messge-passing interface. 
    */
    int ReceiveResultsFromChildProcesses(bool firstBatch = false)
    {
        #ifdef MPI_DEBUG
            cout << "master receiving results from child processes...." << endl;
        #endif
        int numSpectra;
        int sourceProcess;

        Timer ResultsTime( true );
        float totalResultsTime = 0.01f;
        float lastUpdate = 0.0f;

        // For each existing process
        for( int p=0; p < g_numChildren; ++p )
        {
            // Get the process ID that is sending the results.
            MPI_Recv( &sourceProcess,        1,        MPI_INT,    MPI_ANY_SOURCE,    0xEE, MPI_COMM_WORLD, &st );

            #ifdef MPI_DEBUG
                cout << g_hostString << " is receiving search results." << endl;
                Timer receiveTime(true);
            #endif

            // Get the results
            string pack;
            int len;

            MPI_Recv( &len,                    1,        MPI_INT,    sourceProcess,    0x00, MPI_COMM_WORLD, &st );
            pack.resize( len );
            MPI_Recv( (void*) pack.data(),    len,    MPI_CHAR,    sourceProcess,    0x01, MPI_COMM_WORLD, &st );

            // Decompress and deserialize the data stream into objects.
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
                SearchStatistics childSearchStats;
                packArchive & numSpectra;
                packArchive & childSearchStats;
                if(firstBatch)
                {
                    searchStatistics = searchStatistics + childSearchStats;
                }
                else 
                {
                    searchStatistics.numCandidatesQueried += childSearchStats.numCandidatesQueried;
                    searchStatistics.numComparisonsDone += childSearchStats.numComparisonsDone;
                    searchStatistics.numCandidatesSkipped += childSearchStats.numCandidatesSkipped;
                }

                #ifdef MPI_DEBUG
                    cout << g_hostString << " is unpacking results for " << numSpectra << " spectra." << endl;
                #endif
                // For each spectrum in the list
                for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
                {
                    Spectrum* childSpectrum = new Spectrum;
                    Spectrum* rootSpectrum = *sItr;
                    // Get the spectrum and update the stats of total number of comparisons
                    // done and total processing time
                    packArchive & *childSpectrum;
                    //#ifdef MPI_DEBUG
                    //    cout << "Merging results of " << childSpectrum->id.toString() << " with " 
                    //    << rootSpectrum->id.toString() << endl;
                    //#endif
                    rootSpectrum->numTargetComparisons += childSpectrum->numTargetComparisons;
                    rootSpectrum->numDecoyComparisons += childSpectrum->numDecoyComparisons;
                    rootSpectrum->processingTime += childSpectrum->processingTime;
                    rootSpectrum->detailedCompStats = rootSpectrum->detailedCompStats + childSpectrum->detailedCompStats;

                    rootSpectrum->resultsByCharge.resize(childSpectrum->resultsByCharge.size());
                    for (size_t z=0; z < childSpectrum->resultsByCharge.size(); ++z)
                    {
                        Spectrum::SearchResultSetType& rootResults = rootSpectrum->resultsByCharge[z];
                        Spectrum::SearchResultSetType& childResults = childSpectrum->resultsByCharge[z];

                        BOOST_FOREACH(const Spectrum::SearchResultPtr& result, childResults)
                            rootResults.add( result );

                        if (childResults.bestFullySpecificTarget().get()) rootResults.add(childResults.bestFullySpecificTarget());
                        if (childResults.bestFullySpecificDecoy().get()) rootResults.add(childResults.bestFullySpecificDecoy());
                        if (childResults.bestSemiSpecificTarget().get()) rootResults.add(childResults.bestSemiSpecificTarget());
                        if (childResults.bestSemiSpecificDecoy().get()) rootResults.add(childResults.bestSemiSpecificDecoy());
                        if (childResults.bestNonSpecificTarget().get()) rootResults.add(childResults.bestNonSpecificTarget());
                        if (childResults.bestNonSpecificDecoy().get()) rootResults.add(childResults.bestNonSpecificDecoy());
                    }

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

            // Print out the stats
            totalResultsTime = ResultsTime.TimeElapsed();
            if( ( totalResultsTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || p+1 == g_numChildren )
            {
                float nodesPerSec = float(p+1) / totalResultsTime;
                float estimatedTimeRemaining = float(g_numChildren-p-1) / nodesPerSec;
                cout << "Received results from " << p+1 << " of " << g_numChildren << " worker nodes; " << nodesPerSec <<
                        " per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
                lastUpdate = totalResultsTime;
            }

        }

        return 0;
    }
}
}
#endif
