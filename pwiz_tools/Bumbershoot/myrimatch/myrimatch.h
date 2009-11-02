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
// The Original Code is the MyriMatch search engine.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

#ifndef _MYRIMATCH_H
#define _MYRIMATCH_H

#include "stdafx.h"
#include "freicore.h"
#include "base64.h"
#include "myrimatchSpectrum.h"
#include <boost/cstdint.hpp>


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
    struct Version
    {
        static int Major();
        static int Minor();
        static int Revision();
        static std::string str();
        static std::string LastModified();
    };

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

	/**
		This class takes a mass tolerance and mass units and uses them to compare
		two precursor masses.
	*/
	class SpectraMassMapComparator {
		// Mass Tol
		double massTolerance;
		// Daltons or PPM
		MassUnits units;
	public:
		SpectraMassMapComparator(){}
		
		// Init
		SpectraMassMapComparator(double tol, MassUnits unts) 
		{
			massTolerance = tol;
			units = unts;
		}
		
		bool operator()(const double lhs, const double rhs) const {
			float delta = ComputeMassError(lhs,rhs,units);
			if(delta > massTolerance) {
				return lhs < rhs;
			} else {
				return false;
			}
		}
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
