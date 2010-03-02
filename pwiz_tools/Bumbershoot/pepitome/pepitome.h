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
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s):
//

#ifndef _PEPITOME_H
#define _PEPITOME_H

#include "stdafx.h"
#include "freicore.h"
#include "base64.h"
#include "pepitomeSpectrum.h"
#include <boost/cstdint.hpp>


#define PEPITOME_LICENSE			COMMON_LICENSE

using namespace freicore;

namespace freicore
{

namespace pepitome
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
			numSpectraSearched(0), numSpectraQueried(0),
			numComparisonsDone(0), numCandidatesSkipped (0)  {}
        boost::int64_t numSpectraSearched;
		boost::int64_t numSpectraQueried;
		boost::int64_t numComparisonsDone;
        boost::int64_t numCandidatesSkipped;

		template< class Archive >
		void serialize( Archive& ar, const unsigned int version )
		{
			ar & numSpectraSearched & numSpectraQueried & numComparisonsDone & numCandidatesSkipped;
		}

		searchStats operator+ ( const searchStats& rhs )
		{
			searchStats tmp;
			tmp.numSpectraSearched = numSpectraSearched + rhs.numSpectraSearched;
			tmp.numSpectraQueried = numSpectraQueried + rhs.numSpectraQueried;
			tmp.numComparisonsDone = numComparisonsDone + rhs.numComparisonsDone;
            tmp.numCandidatesSkipped = numCandidatesSkipped + rhs.numCandidatesSkipped;
			return tmp;
		}

		operator string()
		{
			stringstream s;
			s	<< numSpectraSearched << " spectra; " << numSpectraQueried << " queries; " << numComparisonsDone << " comparisons";
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

	extern WorkerThreadMap					g_workerThreads;
	extern simplethread_mutex_t				resourceMutex;

	extern vector< double >					relativePeakCount;
	extern vector< simplethread_mutex_t >	spectraMutexes;

	extern proteinStore						proteins;
    extern SpectraStore                     librarySpectra;
	extern SpectraList						spectra;
	extern SpectraMassMapList				spectraMassMapsByChargeState;
	extern float							totalSequenceComparisons;

}
}

#endif
