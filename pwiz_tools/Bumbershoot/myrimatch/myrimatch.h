#ifndef _MYRIMATCH_H
#define _MYRIMATCH_H

#include "stdafx.h"
#include "freicore.h"
#include "base64.h"
#include "myrimatchSpectrum.h"
#include <boost/cstdint.hpp>

#define MYRIMATCH_MAJOR				1.
#define MYRIMATCH_MINOR				5.
#define MYRIMATCH_BUILD				7

#define MYRIMATCH_VERSION			BOOST_PP_CAT( MYRIMATCH_MAJOR, BOOST_PP_CAT( MYRIMATCH_MINOR, MYRIMATCH_BUILD ) )
#define MYRIMATCH_VERSION_STRING	BOOST_PP_STRINGIZE( MYRIMATCH_VERSION )
#define MYRIMATCH_BUILD_DATE		"4/20/2009"

#define MYRIMATCH_LICENSE			COMMON_LICENSE

using namespace freicore;

namespace freicore
{
    #ifdef USE_MPI
        extern MPI_Status st;
        extern void* g_mpiBuffer;
    #endif

namespace myrimatch
{
	struct PrecursorMassChargeKey
	{
		PrecursorMassChargeKey( double m, int z ) : mass(m), charge(z) {}
		bool operator< ( const PrecursorMassChargeKey& rhs ) const
		{
			if( charge == rhs.charge )
				return mass < rhs.mass;
			else
				return charge < rhs.charge;
		}

		double mass;
		int charge;
	};

	typedef multimap< double, SpectraList::iterator >					SpectraMassMap;
	typedef vector< SpectraMassMap >									SpectraMassMapList;

	struct searchStats
	{
		searchStats() :
			numProteinsDigested(0), numCandidatesGenerated(0),
			numCandidatesQueried(0), numComparisonsDone(0), 
            numCandidatesSkipped (0)  {}
        boost::int64_t numProteinsDigested;
		boost::int64_t numCandidatesGenerated;
		boost::int64_t numCandidatesQueried;
		boost::int64_t numComparisonsDone;
        boost::int64_t numCandidatesSkipped;

		template< class Archive >
		void serialize( Archive& ar, const unsigned int version )
		{
			ar & numProteinsDigested & numCandidatesGenerated & numCandidatesQueried & numComparisonsDone & numCandidatesSkipped;
		}

		searchStats operator+ ( const searchStats& rhs )
		{
			searchStats tmp;
			tmp.numProteinsDigested = numProteinsDigested + rhs.numProteinsDigested;
			tmp.numCandidatesGenerated = numCandidatesGenerated + rhs.numCandidatesGenerated;
			tmp.numCandidatesQueried = numCandidatesQueried + rhs.numCandidatesQueried;
			tmp.numComparisonsDone = numComparisonsDone + rhs.numComparisonsDone;
            tmp.numCandidatesSkipped = numCandidatesSkipped + rhs.numCandidatesSkipped;
			return tmp;
		}

		operator string()
		{
			stringstream s;
			s	<< numProteinsDigested << " proteins; " << numCandidatesGenerated << " candidates; "
				<< numCandidatesQueried << " queries; " << numComparisonsDone << " comparisons";
            if(numCandidatesSkipped>0) {
                s << "; " << numCandidatesSkipped << " skipped";
            }
			return s.str();
		}
	};

	struct WorkerInfo : public BaseWorkerInfo
	{
		WorkerInfo( int num, int start, int end ) : BaseWorkerInfo( num, start, end ) {}
		searchStats stats;
	};

	#ifdef USE_MPI
		void TransmitConfigsToChildProcesses();
		void ReceiveConfigsFromRootProcess();
		int ReceivePreparedSpectraFromChildProcesses();
		int TransmitPreparedSpectraToRootProcess( SpectraList& preparedSpectra );
		int ReceiveUnpreparedSpectraBatchFromRootProcess();
		int TransmitUnpreparedSpectraToChildProcesses();
		int ReceiveSpectraFromRootProcess();
		int TransmitSpectraToChildProcesses();
		int TransmitProteinsToChildProcesses();
		int ReceiveProteinBatchFromRootProcess( int lastQueryCount );
		int TransmitResultsToRootProcess( const searchStats& stats );
		int ReceiveResultsFromChildProcesses( searchStats& overallSearchStats );
	#endif

	extern WorkerThreadMap					g_workerThreads;
	extern simplethread_mutex_t				resourceMutex;

	extern vector< double >					relativePeakCount;
	extern vector< simplethread_mutex_t >	spectraMutexes;

	extern proteinStore						proteins;
	extern SpectraList						spectra;
	extern SpectraMassMapList				spectraMassMapsByChargeState;
	extern float							totalSequenceComparisons;

}
}

#endif
