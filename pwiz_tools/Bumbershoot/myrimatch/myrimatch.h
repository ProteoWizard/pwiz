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
// The Original Code is the MyriMatch search engine.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#ifndef _MYRIMATCH_H
#define _MYRIMATCH_H

#include "stdafx.h"
#include "freicore.h"
#include "myrimatchSpectrum.h"
#include <boost/atomic.hpp>
#include <boost/cstdint.hpp>


#define MYRIMATCH_LICENSE            COMMON_LICENSE

using namespace freicore;
using boost::container::flat_multimap;

namespace freicore
{
    #ifdef USE_MPI
        extern MPI_Status st;
        extern void* g_mpiBuffer;
    #endif

namespace myrimatch
{
    struct SearchStatistics
    {
        SearchStatistics()
        :   numProteinsDigested(0),
            numPeptidesGenerated(0),
            numVariantsGenerated(0),
            numComparisonsDone(0),
            numPeptidesSkipped(0)
        {}

        SearchStatistics(const SearchStatistics& other)
        {
            operator=(other);
        }

        SearchStatistics& operator=(const SearchStatistics& other)
        {
            numProteinsDigested.store(other.numProteinsDigested);
            numPeptidesGenerated.store(other.numPeptidesGenerated);
            numVariantsGenerated.store(other.numVariantsGenerated);
            numComparisonsDone.store(other.numComparisonsDone);
            numPeptidesSkipped.store(other.numPeptidesSkipped);
            return *this;
        }

        boost::atomic_uint32_t numProteinsDigested;
        boost::atomic_uint64_t numPeptidesGenerated;
        boost::atomic_uint64_t numVariantsGenerated;
        boost::atomic_uint64_t numComparisonsDone;
        boost::atomic_uint64_t numPeptidesSkipped;

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & numProteinsDigested & numPeptidesGenerated & numVariantsGenerated
               & numComparisonsDone & numPeptidesSkipped;
        }

        SearchStatistics operator+ ( const SearchStatistics& rhs )
        {
            SearchStatistics tmp(*this);
            tmp.numProteinsDigested.fetch_add(rhs.numProteinsDigested);
            tmp.numPeptidesGenerated.fetch_add(rhs.numPeptidesGenerated);
            tmp.numVariantsGenerated.fetch_add(rhs.numVariantsGenerated);
            tmp.numComparisonsDone.fetch_add(rhs.numComparisonsDone);
            tmp.numPeptidesSkipped.fetch_add(rhs.numPeptidesSkipped);
            return tmp;
        }

        void reset()
        {
            numProteinsDigested = 0;
            numPeptidesGenerated = 0;
            numVariantsGenerated = 0;
            numComparisonsDone = 0;
            numPeptidesSkipped = 0;
        }

        operator string()
        {
            stringstream s;
            s    << numProteinsDigested << " proteins; "
                << numPeptidesGenerated << " peptides; "
                << numVariantsGenerated << " variants; "
                << numComparisonsDone << " comparisons";
            if(numPeptidesSkipped>0) {
                s << "; " << numPeptidesSkipped << " skipped";
            }
            return s.str();
        }
    };

    typedef flat_multimap< double, pair<Spectrum*, PrecursorMassHypothesis> >   SpectraMassMap;
    typedef vector< SpectraMassMap >        SpectraMassMapList;


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
        int ReceiveProteinBatchFromRootProcess();
        int TransmitResultsToRootProcess();
        int ReceiveResultsFromChildProcesses( bool firstBatch );
    #endif

    extern proteinStore                        proteins;
    extern SearchStatistics                 searchStatistics;

    extern SpectraList                        spectra;
    extern SpectraMassMapList                avgSpectraByChargeState;
    extern SpectraMassMapList                monoSpectraByChargeState;
    extern float                            totalSequenceComparisons;

}
}

#endif
