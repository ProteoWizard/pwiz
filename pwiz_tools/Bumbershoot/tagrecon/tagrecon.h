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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#ifndef _TAGRECON_H
#define _TAGRECON_H

#include "stdafx.h"
#include "freicore.h"
#include "tagreconSpectrum.h"
#include "simplethreads.h"
#include <boost/cstdint.hpp>

#define TAGRECON_LICENSE			COMMON_LICENSE

//#define DEBUG 1

using namespace freicore;

namespace freicore
{
	#ifdef USE_MPI
        extern MPI_Status st;
        extern void* g_mpiBuffer;
    #endif

namespace tagrecon
{
    struct Version
    {
        static int Major();
        static int Minor();
        static int Revision();
        static std::string str();
        static std::string LastModified();
    };

	#ifdef USE_MPI
		void TransmitConfigsToChildProcesses();
		void ReceiveConfigsFromRootProcess();
	#endif

	typedef map< string, simplethread_mutex_t > tagMutexes_t;

	typedef struct spectrumInfo
	{
		vector< string > sequences;
		bool hasCorrectTag;
	} spectrumInfo_t;

	typedef map< float, string >				modMap_t;

	typedef multimap< float, SpectraList::iterator >					SpectraMassMap;
	typedef vector< SpectraMassMap >									SpectraMassMapList;

	/**
		Structure TagSetInfo stores the spectrum, tag sequence, n-terminal and c-terminal
		masses that sourround the tag.
	*/
	struct TagSetInfo
	{
		TagSetInfo( const SpectraList::iterator& itr, string tag, float nT, float cT ) { 
			sItr = itr;
			nTerminusMass = nT;
			cTerminusMass = cT;
			candidateTag = tag;
		}

		TagSetInfo(string tag, float nT, float cT) {
			candidateTag = tag;
			nTerminusMass = nT;
			cTerminusMass = cT;
		}

        template< class Archive >
		void serialize( Archive& ar, const unsigned int version )
		{
			ar & candidateTag & nTerminusMass & cTerminusMass & tagChargeState & sItr;
		}

		SpectraList::iterator sItr;
		float nTerminusMass;
		float cTerminusMass;
		string candidateTag;
        int tagChargeState;
	};

	/**
		Class TagMapCompare sorts tag to spectrum map based on spectral similarity.
		Two spectrum are said to be similar if the tag sequences match and also 
		the total mass deviation between n-terminal and c-terminal masses that
		sourround the tags is <= +/-maxDeviation. This comparator essentially 
		sorts similar tags together. 
	*/
	class TagSetCompare {

		// Maximum deviation observed between the terminal masses
		// the sourround a tag match.
		float maxDeviation;

	public:
		TagSetCompare(float maxDeviation = 300.0f) : maxDeviation(maxDeviation) {};

		/**
			operator () sorts the tags based on tag sequence first. If two tag sequences
			match then we cluster them based on their total terminal mass deviation from each
			other. Spectra with tags that have total terminal deviations <= +/-maxDeviation
			are kept together. If two tags don't satisfy this criterion then they are
			sorted based on their n-terminal masses.
		*/
		bool operator ()(const TagSetInfo& lhs, const TagSetInfo& rhs) const {
			if(lhs.candidateTag < rhs.candidateTag) {
				return lhs.candidateTag < rhs.candidateTag;
			} else if(lhs.candidateTag > rhs.candidateTag) {
				return lhs.candidateTag < rhs.candidateTag;
			} else {
				float nTerminalAbsMassDiff = fabs(lhs.nTerminusMass-rhs.nTerminusMass);
				float cTerminalAbsMassDiff =  fabs(lhs.cTerminusMass-rhs.cTerminusMass);
				//if((nTerminalAbsMassDiff+cTerminalAbsMassDiff) > maxDeviation) {
                if(nTerminalAbsMassDiff > maxDeviation && cTerminalAbsMassDiff > maxDeviation) {
					return lhs.nTerminusMass < rhs.nTerminusMass;
				} else {
					return false;
				}
			}
		}
        
	};

	// A spectra to tag map (tag to spectrum) that sorts tags based on spectral similarity
	typedef multiset< TagSetInfo, TagSetCompare >						SpectraTagMap;
	//typedef multimap<pair <string, float>, TagMapInfo>				SpectraTagMap;
	typedef vector< SpectraTagMap >										SpectraTagMapList;

	struct searchStats
	{
		searchStats() :
			numProteinsDigested(0), numCandidatesGenerated(0),
			numCandidatesQueried(0), numComparisonsDone(0), 
            numCandidatesSkipped(0) {}
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
		int TransmitSpectraToChildProcesses( int done );
		int TransmitProteinsToChildProcesses();
		int ReceiveProteinBatchFromRootProcess( int lastQueryCount );
		int TransmitResultsToRootProcess( const searchStats& stats );
		int ReceiveResultsFromChildProcesses( searchStats& overallSearchStats, bool firstBatch );
	#endif

	extern WorkerThreadMap	    g_workerThreads;
	extern simplethread_mutex_t	resourceMutex;

	extern proteinStore			proteins;
	extern SpectraList			spectra;
	extern SpectraMassMap		spectraMassMapsByChargeState;
	extern SpectraTagMap		spectraTagMapsByChargeState;
}
}

#endif
