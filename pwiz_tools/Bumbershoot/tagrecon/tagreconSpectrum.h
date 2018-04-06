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

#ifndef _TAGRECONSPECTRUM_H
#define _TAGRECONSPECTRUM_H

#include "stdafx.h"
#include "freicore.h"
#include "tagreconConfig.h"
#include "tagsFile.h"
#include "SearchSpectrum.h"
#include "PeakSpectrum.h"
#include "simplethreads.h"
#include "Histogram.h"
#include <boost/thread/thread.hpp>
#include <boost/thread/mutex.hpp>
#include <bitset>

#define IS_VALID_INDEX(index,length) (index >=0 && index < length ? true : false)

namespace freicore
{
namespace tagrecon
{
    struct PeakInfo
    {
        template< class Archive >
        void serialize( Archive& ar, const int unsigned version )
        {
            ar & intenClass & normalizedIntensity;
        }

        // for MVH
        int        intenClass;

        // for xcorr
        float   normalizedIntensity;
    };

    /**!
        SearchResult data structure is a sub-class of BaseSearchResult.
        The data structure is extended to store additional scores like
        mvh, massError, mzFidelity, expectation value and fdr etc.
    */
    struct SearchResult : public BaseSearchResult
    {
        SearchResult() : BaseSearchResult("A") {}
        SearchResult( const DigestedPeptide& peptide ) : BaseSearchResult(peptide) {}

        bool _isDecoy;
        bool isDecoy() const {return _isDecoy;}

        double mvh;
        double mzFidelity;
        double rankScore;
        double probabilisticScore;

        double mzSSE;
        double massError;

        double XCorr;

        double pvalue;
        double expect;
        double fdr;

        size_t numberOfBlindMods;
        size_t numberOfOtherMods;

        vector<double> matchedIons;

        double getTotalScore() const
        {
            return rankScore;
        }

        /**!
            getScoreList function makes a SearchScoreList data structure
            the contains all scores for a search result
        */
        SearchScoreList getScoreList() const
        {
            SearchScoreList scoreList;
            scoreList.push_back( SearchScore( "mvh", mvh, MS_MyriMatch_MVH ) );
            scoreList.push_back( SearchScore( "mzSSE", mzSSE ) );
            scoreList.push_back( SearchScore( "mzFidelity", mzFidelity, MS_MyriMatch_mzFidelity) );

            if (g_rtConfig->ComputeXCorr)
                scoreList.push_back( SearchScore( "xcorr", XCorr ) );

            scoreList.push_back( SearchScore( "numDynamicMods", numberOfOtherMods ) );
            
            if (g_rtConfig->unknownMassShiftSearchMode == BLIND_PTMS)
                scoreList.push_back( SearchScore( "numBlindMods", numberOfBlindMods ) );

            return scoreList;
        }

        string& getAnnotation(string& annotation) const
        {
            ostringstream matchedIonsStream;
            for( size_t i=0; i < matchedIons.size(); ++i )
                matchedIonsStream << matchedIons[i] << " ";
            annotation = matchedIonsStream.str();
            return annotation;
        }

        typedef map< string, map < size_t, double > > MinMaxScoresByZState;

        double normalizeScore(string name, double value, size_t charge, MinMaxScoresByZState minScoreMap, MinMaxScoresByZState maxScoreMap)
        {
            double minScore = minScoreMap[name][charge];
            double maxScore = maxScoreMap[name][charge];
            double div = (maxScore-minScore);
            div = div<=0 ? 1 : div;
            return (value-minScore)/div;
        }

        /// Search results are sorted based on MVH. 
        bool operator< ( const SearchResult& rhs ) const
        {
            return mvh < rhs.mvh;
        }

        /// Operator to compare the equality of two search scores (MVH)
        bool operator== ( const SearchResult& rhs ) const
        {
            return mvh == rhs.mvh;
        }

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & boost::serialization::base_object< BaseSearchResult >( *this );
            ar & _isDecoy & mvh & massError & mzSSE & mzFidelity & rankScore & XCorr & probabilisticScore;
            ar & numberOfBlindMods & numberOfOtherMods;
        }
    };

    /**!
        Spectrum extends the PeakSpectrum, SearchSpectrum, and TaggingSpectrum
    */
    struct Spectrum : public PeakSpectrum<PeakInfo>, SearchSpectrum<SearchResult>, TaggingSpectrum
    {
        Spectrum() : mutex(new boost::mutex) {}

        void initialize( int numIntenClasses, int numMzFidelityClasses )
        {
            intenClassCounts.resize( numIntenClasses, 0 );
            mzFidelityThresholds.resize( numMzFidelityClasses, 0 );
        }

        void Preprocess();

        int QuerySequence();

        // for MVH scoring
        void ClassifyPeakIntensities();

        // for XCorr scoring
        void NormalizePeakIntensities();

        /* This function predicts the theoretical spectrum for a result, and computes the
            cross-correlation between the predicted spectrum and the experimental spectrum
        */
        void ComputeXCorrs();
        
        void ScoreSequenceVsSpectrum( SearchResult& result,
                                      const vector< double >& seqIons,
                                      int NTT );

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & boost::serialization::base_object< BaseSpectrum >( *this );
            ar & boost::serialization::base_object< PeakSpectrum< PeakInfo > >( *this );
            ar & boost::serialization::base_object< SearchSpectrum< SearchResult > >( *this );
            ar & boost::serialization::base_object< TaggingSpectrum> (*this);

            ar & intenClassCounts;
            ar & mzFidelityThresholds;
            ar & fragmentTypes;
            //ar & mOfPrecursorList;
            if( g_rtConfig->MakeScoreHistograms )
                ar & scoreHistogram;
        }

        vector< int >        intenClassCounts;
        vector< double >    mzFidelityThresholds;
        FragmentTypesBitset fragmentTypes;
        
        vector< float >        mOfPrecursorList;

        /**!
            CalculateMvhProbabilities_R enumerates all possible class combinations
            for a particular class and computes the MVH probability of each combination
            of a class.
        */
        void CalculateMvhProbabilities_R(    const int minValue,
                                            const int totalValue,
                                            const int numClasses,
                                            const vector< int >& classCounts,
                                            vector< double >& mvhProbabilities,
                                            MvIntKey& key,
                                            lnFactorialTable& lnFT )
        {
            // At the highest degree of variability the key is fully set
            // Calculate the MVH score and add it to the mvTable
            if( numClasses == 1 )
            {
                key.front() = totalValue;
                int totalClasses = (int) key.size();
                double lnP = 0.0f;
                for( int i=0; i < totalClasses; ++i )
                    lnP += lnCombin( classCounts[i], key[i], lnFT );
                //float p = 0.0f;
                //for( int i=0; i < totalClasses; ++i )
                //    p += lnCombin( classCounts[i], key[i], lnFT );
                int totalClassCount = accumulate( classCounts.begin(), classCounts.end(), 0 );
                int totalValueCount = accumulate( key.begin(), key.end(), 0 );
                lnP -= lnCombin( totalClassCount, totalValueCount, lnFT );
                START_PROFILER(9);
                mvhProbabilities.push_back( lnP );
                STOP_PROFILER(9);

            // Create another level of variability
            } else
            {
                for( int curValue = minValue; (totalValue - curValue) >= minValue ; ++curValue )
                {
                    key[numClasses-1] = curValue;
                    CalculateMvhProbabilities_R( minValue, totalValue - curValue, numClasses-1, classCounts, mvhProbabilities, key, lnFT );
                }
            }
        }

        typedef map< float, double > PValueByMvhProbability;

        /**!
            CalculateMvhProbabilities computes the probability of a match occuring by random match
            using a multi-variate hypergeometric distribution. The function can be used to compute
            MVH probability using rank-based intensity scoring system or rank based m/z fidelity 
            scoring system. See article MyriMatch article in Journal of Proteome Research; 2007; 
            6; 654-661 for additional details of the MVH distribution based p-value computation.
        */
        void CalculateMvhProbabilities(    const int minValue /* minimum value of all classes */,
                                        const int totalValue /* total value of all classes */,
                                        const int numClasses /* total number of classes */,
                                        vector< int > classCounts /* individual class counts */,
                                        PValueByMvhProbability& pValueByMvhProbability /* A pointer to store 
                                                                                          the computed p-value */,
                                        lnFactorialTable& lnFT /* Precomputed ln(n!) table */)
        {
            // Check to see if all classes have a count of at least 1
            bool allClassesUsed = true;
            for( int i=0; i < numClasses; ++i )
            {
                if( classCounts[i] == 0 )
                {
                    allClassesUsed = false;
                    break;
                }
            }

            // If any class is not populated, increment each class by one
            if( !allClassesUsed )
                for( int i=0; i < numClasses; ++i )
                    ++ classCounts[i];

            MvIntKey key;
            key.resize( numClasses, 0 );
            vector< double > mvhProbabilities;
            START_PROFILER(10);
            // Compute MVH probabilites
            CalculateMvhProbabilities_R( minValue, totalValue, numClasses, classCounts, mvhProbabilities, key, lnFT );
            STOP_PROFILER(10);

            // Sort them and......
            std::sort( mvhProbabilities.begin(), mvhProbabilities.end() );
            //cout << mvhProbabilities << endl;
            double pSum = exp( mvhProbabilities[0] );
            for( size_t i=1; i < mvhProbabilities.size(); ++i )
            {
                double& curProb = mvhProbabilities[i];
                if( mvhProbabilities[i-1] < curProb )
                {
                    pSum += exp( curProb );
                    pValueByMvhProbability[ - (float) curProb ] = pSum;
                }
            }
        }

        Histogram< float >    scoreHistogram;
        map< float, int > scores;

        boost::shared_ptr<boost::mutex> mutex;
    };

    struct SpectraList : public    PeakSpectraList< Spectrum, SpectraList >,
                                SearchSpectraList< Spectrum, SpectraList >,
                                TaggingSpectraList< Spectrum, SpectraList >
    {
        using BaseSpectraList< Spectrum, SpectraList >::ListIndex;
        using BaseSpectraList< Spectrum, SpectraList >::ListIndexIterator;
    };
}
}

// eliminate serialization overhead at the cost of never being able to increase the version.
BOOST_CLASS_IMPLEMENTATION( freicore::tagrecon::Spectrum, boost::serialization::object_serializable );

// eliminate object tracking at the risk of a programming error creating duplicate objects.
BOOST_CLASS_TRACKING( freicore::tagrecon::Spectrum, boost::serialization::track_never )

#endif
