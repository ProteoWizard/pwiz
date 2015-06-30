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

#ifndef _SHARED_DEFS_H
#define _SHARED_DEFS_H

#include "stdafx.h"
#include "shared_types.h"
#include "ResidueMap.h"
#include "lnFactorialTable.h"
#include "BaseRunTimeConfig.h"
#include "simplethreads.h"
#include "Profiler.h"

//#define BUMBERSHOOT_PROFILING

/*#ifndef NO_MPI
#    define    USE_MPI
#endif*/

#if defined USE_MPI
#    undef    SEEK_SET
#    undef    SEEK_CUR
#    undef    SEEK_END
#    include "mpi.h"

#    define    MPI_BUFFER_SIZE    8388608
#endif

#define READ_BUFFER_SIZE    16384 // 1048576
#define WRITE_BUFFER_SIZE    1048576

#define STR_EQUAL( l, r )    ( string(l) == string(r) )
#define SAFEDELETE( var ) { if( var != NULL ) { delete var; var = NULL; } }

#define COMMON_LICENSE        "Vanderbilt University (c) 2012, D.Tabb/M.Chambers/S.Dasari\n" \
                            "Licensed under the Apache License, Version 2.0\n"

#define COMMON_RTCONFIG \
    RTCONFIG_VARIABLE( string,                WorkingDirectory,            ""                    ) \
    RTCONFIG_VARIABLE( int,                    NumChargeStates,            3                    ) \
    RTCONFIG_VARIABLE( double,                StatusUpdateFrequency,        5                    )

#define MULTITHREAD_RTCONFIG \
    RTCONFIG_VARIABLE( bool,                UseMultipleProcessors,        true                )

#define SPECTRUM_RTCONFIG \
    RTCONFIG_VARIABLE( double,                PrecursorMzTolerance,        1.5                    ) \
    RTCONFIG_VARIABLE( string,                PrecursorMzToleranceUnits,    "daltons"            ) \
    RTCONFIG_VARIABLE( double,                FragmentMzTolerance,        0.5                    ) \
    RTCONFIG_VARIABLE( string,                FragmentMzToleranceUnits,    "daltons"            ) \
    RTCONFIG_VARIABLE( double,                ComplementMzTolerance,        0.5                    ) \
    RTCONFIG_VARIABLE( double,                IsotopeMzTolerance,            0.25                ) \
    RTCONFIG_VARIABLE( bool,                DuplicateSpectra,            true                ) \
    RTCONFIG_VARIABLE( bool,                UseSmartPlusThreeModel,        true                ) \
    RTCONFIG_VARIABLE( bool,                UseChargeStateFromMS,        false                )

#define SEQUENCE_RTCONFIG \
    RTCONFIG_VARIABLE( string,                DynamicMods,                ""                    ) \
    RTCONFIG_VARIABLE( int,                    MaxDynamicMods,                2                    ) \
    RTCONFIG_VARIABLE( string,                StaticMods,                    ""                    ) \
    RTCONFIG_VARIABLE( bool,                UseAvgMassOfSequences,        false                ) \
    RTCONFIG_VARIABLE( int,                    MaxNumPeptideVariants,        1000000                )

#define VALIDATION_RTCONFIG \
    RTCONFIG_VARIABLE( string,                DecoyPrefix,                "rev_"                ) \
    RTCONFIG_VARIABLE( double,                MaxFDR,                        0.25                ) \
    RTCONFIG_VARIABLE( double,                NTerminusMassTolerance,        2.5                 ) \
    RTCONFIG_VARIABLE( double,                CTerminusMassTolerance,        1.0                    ) 
    

namespace std
{
    ostream&        operator<< ( ostream& o, const freicore::SpectrumId& s );

    istream&        operator>> ( istream& i, freicore::DynamicMod& rhs );
    istream&        operator>> ( istream& i, freicore::StaticMod& rhs );
    ostream&        operator<< ( ostream& o, const freicore::DynamicMod& rhs );
    ostream&        operator<< ( ostream& o, const freicore::StaticMod& rhs );
}

namespace freicore
{

    extern fileList_t            g_inputFilenames;

    extern string                g_dbFilename;            // name of FASTA file, e.g. "file.fasta"
    extern string                g_dbPath;                // path to FASTA database, e.g. "/dir"
    extern string               g_spectralLibName;

    extern ResidueMap*            g_residueMap;
    extern BaseRunTimeConfig*    g_rtSharedConfig;
    extern lnFactorialTable        g_lnFactorialTable;

    extern bool                    g_NormalizeOnMode;

    extern int                    g_pid;
    extern int                    g_endianType;
    extern int                    g_numProcesses;            // total number of processes in the MPI job
    extern int                    g_numChildren;            // number of worker processes in the MPI job
    extern int                    g_numWorkers;            // number of worker threads in the process
    extern string                g_hostString;

    extern vector< Profiler >    g_profilers;

    #ifdef BUMBERSHOOT_PROFILING
        #define START_PROFILER(n) g_profilers[n].Begin(false);
        #define STOP_PROFILER(n) g_profilers[n].End();
        #define INIT_PROFILERS(n) \
            g_profilers.resize(n); \
            for( size_t i=0; i < g_profilers.size(); ++i ) { \
                g_profilers[i].Begin(); \
                g_profilers[i].End(); \
            }
        #define PRINT_PROFILERS(stream,prefix) \
            stream << prefix; \
            for( size_t i=0; i < g_profilers.size(); ++i ) \
                cout << "; " << i << ":" << g_profilers[i].End(); \
            cout << endl;
    #else
        #define START_PROFILER(n) // noop
        #define STOP_PROFILER(n) // noop
        #define INIT_PROFILERS(n) // noop
        #define PRINT_PROFILERS(stream,prefix) // noop
    #endif

    /*#ifdef USE_MPI
        extern MPI_Status        st;
        extern void*            g_mpiBuffer;
        extern MPI_Datatype        mpi_flatSpectrum;
        extern MPI_Datatype        mpi_flatPeakData;
        extern MPI_Datatype        mpi_flatTagInfo;
    #endif*/


    struct BaseWorkerInfo
    {
        BaseWorkerInfo() {}
        BaseWorkerInfo( int num, int start, int end ) : workerNum(num), startIndex(start), endIndex(end)
        {
            char buf[256];
            sprintf( buf, "Process #%d:%d (%s)", g_pid, workerNum, GetHostname().c_str() );
            workerHostString = buf;
        }

        virtual ~BaseWorkerInfo() {}

        string workerHostString;
        int workerNum;
        int startIndex;
        int endIndex;
        map< string, void* > data;
    };

    struct WorkerThreadMap : public map< simplethread_id_t, BaseWorkerInfo* >
    {
        void clear()
        {
            for( map< simplethread_id_t, BaseWorkerInfo* >::iterator itr = begin(); itr != end(); ++itr )
                delete itr->second;
            map< simplethread_id_t, BaseWorkerInfo* >::clear();
        }
    };

    template< class keyT >
    class MvKey : public vector< keyT >
    {
    public:
        typedef keyT key_type;

        MvKey(    key_type v0 = -1, key_type v1 = -1, key_type v2 = -1, key_type v3 = -1, key_type v4 = -1,
                key_type v5 = -1, key_type v6 = -1, key_type v7 = -1, key_type v8 = -1, key_type v9 = -1 )
        {
            if (v0 > -1) this->push_back(v0);
            if (v1 > -1) this->push_back(v1);
            if (v2 > -1) this->push_back(v2);
            if (v3 > -1) this->push_back(v3);
            if (v4 > -1) this->push_back(v4);
            if (v5 > -1) this->push_back(v5);
            if (v6 > -1) this->push_back(v6);
            if (v7 > -1) this->push_back(v7);
            if (v8 > -1) this->push_back(v8);
            if (v9 > -1) this->push_back(v9);

            //for( int i=0; i < (int) size(); ++i )
            //    cout << at(i) << " ";
            //cout << endl;
        }

        //MvKey( vector< key_type >::iterator first, vector< key_type >::iterator last ) : vector< key_type >()
        //{}

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & boost::serialization::base_object< vector< key_type > >( *this );
        }

        void incrementClass( size_t v )
        {
            if( v >= vector< key_type >::size() )
                vector< key_type >::resize( v+1, 0 );
            ++ vector< key_type >::at( v );
        }

        string toString() const
        {
            stringstream s;

            s << "( ";
            for( size_t i=0; i < vector< key_type >::size(); ++i )
                s << vector< key_type >::at( i ) << " ";
            s << ")";
            return s.str();
        }
    };

    template< class keyT >
    struct MvKeyLessThan
    {
        bool operator() ( const MvKey< keyT >& l, const MvKey< keyT >& r ) const
        {
            if( l.size() != r.size() )
            {
                cerr << "mvKeyLessThan: comparing mvKeys of different size (lhs: " <<
                        l.size() << "; rhs: " << r.size() << ")" << endl;
                return false;
            }

            for( size_t i=0; i < l.size(); ++i )
            {
                if( l[i] != r[i] )
                    return l[i] < r[i];
            }
            return false;
        }
    };

    template< class keyT, class valueT >
    class MvMap : public map< MvKey< keyT >, valueT, MvKeyLessThan< keyT > >
    {
        typedef keyT    key_type;
        typedef valueT    value_type;

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & boost::serialization::base_object< map< MvKey< keyT >, valueT, MvKeyLessThan< keyT > > >( *this );
        }
    };

    typedef MvKey< int >                            MvIntKey;
    class MvhTable : public MvMap< int, double >
    {
        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & boost::serialization::base_object< MvMap< int, double > >( *this );
        }
    public:
        void ConvertToPValues();
    };

    typedef size_t                                    tagInstancesOffset_t;

    typedef struct tagInstance
    {
        tagInstance( long idx, short off ) :
            proteinIndex(idx),
            dataOffset(off) {}
        ProteinIndex    proteinIndex;
        ProteinOffset    dataOffset;
    } tagInstance_t;

    typedef struct tagInstanceMetaData
    {
        tagInstanceMetaData() : offset(0), size(0), proportion(0) {}
        tagInstancesOffset_t    offset;
        int                        size;
        float                    proportion;
    } tagMetaInstance_t;

    typedef vector< tagInstance_t >                tagInstances_t;
    typedef map< string, tagInstances_t >        tagIndex_t;

    typedef struct tagMetaIndex : public map< string, tagMetaInstance_t >
    {
        tagMetaIndex() : map< string, tagMetaInstance_t >(), totalTagInstances(0) {}
        int                        totalTagInstances;
    } tagMetaIndex_t;

    // Simple macro substitution to clean up code
    #define PACKVARS packbuf,packsize,&packoffset
    #define PACKVARS_ARRAYS(i) packbuf[i],packsize[i],&packoffset[i]
}

// eliminate serialization overhead at the cost of never being able to increase the version.
BOOST_CLASS_IMPLEMENTATION( freicore::MvIntKey, boost::serialization::object_serializable )
BOOST_CLASS_IMPLEMENTATION( freicore::MvhTable, boost::serialization::object_serializable )

// eliminate object tracking at the risk of a programming error creating duplicate objects.
BOOST_CLASS_TRACKING( freicore::MvIntKey, boost::serialization::track_never )
BOOST_CLASS_TRACKING( freicore::MvhTable, boost::serialization::track_never )

#endif
