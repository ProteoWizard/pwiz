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

#ifndef _TAGRECON_H
#define _TAGRECON_H

#include "stdafx.h"
#include "freicore.h"
#include "tagreconSpectrum.h"
#include "AhoCorasickTrie.hpp"
#include <boost/atomic.hpp>
#include <boost/cstdint.hpp>
#include <boost/interprocess/containers/container/flat_map.hpp>
#include <boost/interprocess/containers/container/flat_set.hpp>

#define TAGRECON_LICENSE            COMMON_LICENSE

//#define DEBUG 1

using namespace freicore;
using boost::container::flat_map;
using boost::container::flat_multimap;
using boost::container::flat_set;
using boost::container::flat_multiset;

namespace freicore
{
    #ifdef USE_MPI
        extern MPI_Status st;
        extern void* g_mpiBuffer;
    #endif

namespace tagrecon
{
    typedef struct spectrumInfo
    {
        vector< string > sequences;
        bool hasCorrectTag;
    } spectrumInfo_t;

    typedef flat_map< float, string >                                    modMap_t;

    /**
        Structure TagSetInfo stores the spectrum, tag sequence, n-terminal and c-terminal
        masses that sourround the tag.
    */
    struct TagSpectrumInfo
    {
        TagSpectrumInfo( const SpectraList::iterator& itr, string tag, float nT, float cT ) { 
            sItr = itr;
            nTerminusMass = nT;
            cTerminusMass = cT;
            candidateTag = tag;
        }

        TagSpectrumInfo(string tag, float nT, float cT) {
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

    struct TagMatchInfo
    {

        TagMatchInfo() {}

        TagMatchInfo(Spectrum* spec, float modMass, TermMassMatch nTerm, TermMassMatch cTerm)
        {
            spectrum = spec;
            modificationMass = modMass;
            nTermMatch = nTerm;
            cTermMatch = cTerm;
            // Set later for modification localization.
            lowIndex = 0;
            highIndex = 0;
            modMassTolerance = 0.0f;
        }

        bool operator < (const TagMatchInfo& rhs) const
        {
            if(modificationMass == rhs.modificationMass)
                if(nTermMatch == rhs.nTermMatch)
                    if(cTermMatch == rhs.cTermMatch)
                        return spectrum->id < rhs.spectrum->id;
                    else
                        return cTermMatch < rhs.cTermMatch;
                else
                    return nTermMatch < rhs.nTermMatch;
            else
                return modificationMass < rhs.modificationMass;
        }

        Spectrum* spectrum;
        float modificationMass;
        TermMassMatch nTermMatch;
        TermMassMatch cTermMatch;
        // Mod localization variables
        size_t lowIndex;
        size_t highIndex;
        float modMassTolerance;
    };

    struct AATagToSpectraMap
    {
        string aminoAcidTag;
        vector<TagSpectrumInfo> tags;

        AATagToSpectraMap()
        {
            aminoAcidTag = "";
        }

        AATagToSpectraMap(const string& aaTag)
        {
            aminoAcidTag = aaTag;
        }

        void addTag(const TagSpectrumInfo& tag)
        {
            tags.push_back(tag);
        }

        bool operator < (const AATagToSpectraMap& rhs)
        {
            return rhs.aminoAcidTag < aminoAcidTag;
        }

        bool operator== (const AATagToSpectraMap& rhs)
        {
            return (aminoAcidTag.compare(rhs.aminoAcidTag)==0);
        }  

        operator const string&() const {return aminoAcidTag;}
    };

    struct AATagToSpectraMapCompare
    {
        bool operator() (const shared_ptr<AATagToSpectraMap>& lhs, const shared_ptr<AATagToSpectraMap>& rhs) const
        {
            return *lhs < *rhs;
        }
    };

    typedef AhoCorasickTrie<ascii_translator, AATagToSpectraMap>         SpectraTagTrie;

    struct SearchStatistics
    {
        SearchStatistics()
        :   numProteinsDigested(0),
            numCandidatesGenerated(0),
            numCandidatesQueried(0),
            numComparisonsDone(0),
            numCandidatesSkipped(0)
        {}

        SearchStatistics(const SearchStatistics& other)
        {
            operator=(other);
        }

        SearchStatistics& operator=(const SearchStatistics& other)
        {
            numProteinsDigested.store(other.numProteinsDigested);
            numCandidatesGenerated.store(other.numCandidatesGenerated);
            numCandidatesQueried.store(other.numCandidatesQueried);
            numComparisonsDone.store(other.numComparisonsDone);
            numCandidatesSkipped.store(other.numCandidatesSkipped);
            return *this;
        }

        boost::atomic_uint32_t numProteinsDigested;
        boost::atomic_uint64_t numCandidatesGenerated;
        boost::atomic_uint64_t numCandidatesQueried;
        boost::atomic_uint64_t numComparisonsDone;
        boost::atomic_uint64_t numCandidatesSkipped;

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & numProteinsDigested & numCandidatesGenerated & numCandidatesQueried & numComparisonsDone & numCandidatesSkipped;
        }

        SearchStatistics operator+ ( const SearchStatistics& rhs )
        {
            SearchStatistics tmp(*this);
            tmp.numProteinsDigested.fetch_add(rhs.numProteinsDigested);
            tmp.numCandidatesGenerated.fetch_add(rhs.numCandidatesGenerated);
            tmp.numCandidatesQueried.fetch_add(rhs.numCandidatesQueried);
            tmp.numComparisonsDone.fetch_add(rhs.numComparisonsDone);
            tmp.numCandidatesSkipped.fetch_add(rhs.numCandidatesSkipped);
            return tmp;
        }

        operator string()
        {
            stringstream s;
            s    << numProteinsDigested << " proteins; " << numCandidatesGenerated << " candidates; "
                << numCandidatesQueried << " queries; " << numComparisonsDone << " comparisons";
            if(numCandidatesSkipped>0) {
                s << "; " << numCandidatesSkipped << " skipped";
            }
            return s.str();
        }
    };

    typedef flat_multimap< double, pair<Spectrum*, PrecursorMassHypothesis> >   SpectraMassMap;
    typedef vector< SpectraMassMap >        SpectraMassMapList;

    #ifdef USE_MPI
        void TransmitConfigsToChildProcesses();
        void ReceiveConfigsFromRootProcess();
        void ReceiveNETRewardsFromRootProcess();
        void TransmitNETRewardsToChildProcess();
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

    extern proteinStore         proteins;
    extern SearchStatistics     searchStatistics;

    extern SpectraList            spectra;
    // These lists hold precursor masses for "untagged" spectra.
    extern SpectraMassMapList    untaggedSpectraByChargeState;
}
}

#endif
