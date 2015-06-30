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

#ifndef _MYRIMATCHSPECTRUM_H
#define _MYRIMATCHSPECTRUM_H

#include "stdafx.h"
#include "freicore.h"
#include "myrimatchConfig.h"
#include "SearchSpectrum.h"
#include "PeakSpectrum.h"
#include "Histogram.h"
#include <boost/thread/thread.hpp>
#include <boost/thread/mutex.hpp>
#include <bitset>

namespace freicore
{
namespace myrimatch
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

    typedef BasePeakData< PeakInfo > PeakData;

    struct SearchResult : public BaseSearchResult
    {
        SearchResult() : BaseSearchResult("A") {}
        SearchResult( const DigestedPeptide& c ) : BaseSearchResult( c ) {}

        bool _isDecoy;
        bool isDecoy() const {return _isDecoy;}

        double mvh;
        //double massError;
        double mzFidelity;
        //double pvalue;
        //double expect;
        //double fdr;
        //double newMZFidelity;
        //double mzSSE;
        //double mzMAE;
        
        double XCorr;

        /*double deltaMVHAvg;
        double deltaMVHMode;
        double deltaMVHSeqType;
        double deltaMVHSmartSeqType;
        
        double deltaMZFidelityAvg;
        double deltaMZFidelityMode;
        double deltaMZFidelitySeqType;
        double deltaMZFidelitySmartSeqType;

        double mvhMode;
        double mzFidelityMode;*/

        vector<double> matchedIons;

        double getTotalScore() const
        {
            return mvh;
        }

        SearchScoreList getScoreList() const
        {
            SearchScoreList scoreList;
            scoreList.push_back( SearchScore( "mvh", mvh, MS_MyriMatch_MVH ) );
            //scoreList.push_back( SearchScore( "massError", massError ) );
            //scoreList.push_back( SearchScore( "mzSSE", mzSSE ) );
            scoreList.push_back( SearchScore( "mzFidelity", mzFidelity, MS_MyriMatch_mzFidelity ) );
            //scoreList.push_back( SearchScore( "newMZFidelity" , newMZFidelity ) );
            //scoreList.push_back( SearchScore( "mzMAE", mzMAE ) ); 

            if (g_rtConfig->ComputeXCorr)
                scoreList.push_back( SearchScore( "xcorr", XCorr ) ); // not really a Sequest score

            /*if( g_rtConfig->CalculateRelativeScores )
            {
                scoreList.push_back( SearchScore( "pvalue", pvalue ) );
                scoreList.push_back( SearchScore( "expect", expect ) );
            }*/
            
            //scoreList.push_back( SearchScoreInfo( "deltaMVHMode", deltaMVHMode ) );
            //scoreList.push_back( SearchScoreInfo( "deltaMVHAvg", deltaMVHAvg ) );
            //scoreList.push_back( SearchScoreInfo( "deltaMVHSeqType", deltaMVHSeqType ) );
            //scoreList.push_back( SearchScoreInfo( "deltaMVHSmartSeqType", deltaMVHSmartSeqType ) );
            //scoreList.push_back( SearchScoreInfo( "deltaMZFidelityMode", deltaMZFidelityMode ) );
            //scoreList.push_back( SearchScoreInfo( "deltaMZFidelityAvg", deltaMZFidelityAvg ) );
            //scoreList.push_back( SearchScoreInfo( "deltaMZFidelitySeqType", deltaMZFidelitySeqType ) );
            //scoreList.push_back( SearchScoreInfo( "deltaMZFidelitySmartSeqType", deltaMZFidelitySmartSeqType ) );

            //scoreList.push_back( SearchScoreInfo( "mvhMode", mvhMode) );
            //scoreList.push_back( SearchScoreInfo( "mzFidelityMode", mzFidelityMode) );
            //scoreList.push_back( SearchScoreInfo( "deltaMZFidelity", deltaMZFidelity) );

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
            ar & _isDecoy & mvh & mzFidelity & XCorr;
            //ar & massError & mzSSE & mzMAE & newMZFidelity;
            //ar & deltaMVHAvg & deltaMVHMode & deltaMVHSeqType & deltaMVHSmartSeqType;
            //ar & deltaMZFidelityMode & deltaMZFidelityAvg & deltaMZFidelitySeqType & deltaMZFidelitySmartSeqType;
            //ar & mvhMode & mzFidelityMode;
            //if( g_rtConfig->CalculateRelativeScores )
            //    ar & pvalue & expect;
        }
    };

    struct Spectrum : public PeakSpectrum< PeakInfo >, SearchSpectrum< SearchResult >
    {
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

        void computeSecondaryScores();

        void ScoreSequenceVsSpectrum( SearchResult& result, const string& seq, const vector< double >& seqIons );

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & boost::serialization::base_object< BaseSpectrum >( *this );
            ar & boost::serialization::base_object< PeakSpectrum< PeakInfo > >( *this );
            ar & boost::serialization::base_object< SearchSpectrum< SearchResult > >( *this );

            ar & intenClassCounts;
            ar & mzFidelityThresholds;
            ar & fragmentTypes;

            ar & mvhScoreDistribution;
            ar & mzFidelityDistribution;
        }

        vector<int>          intenClassCounts;
        vector<double>       mzFidelityThresholds;
        vector<double>       newMZFidelityThresholds;

        Histogram<double>    scoreHistogram;
        flat_map<double, int>     scores;

        // Keep track of the score distributions
        flat_map<int, int> mvhScoreDistribution;
        flat_map<int, int> mzFidelityDistribution;

        boost::mutex mutex;
    };

    struct SpectraList : public    PeakSpectraList< Spectrum, SpectraList >,
                                SearchSpectraList< Spectrum, SpectraList >
    {
        using BaseSpectraList< Spectrum, SpectraList >::ListIndex;
        using BaseSpectraList< Spectrum, SpectraList >::ListIndexIterator;
    };
}
}

// eliminate serialization overhead at the cost of never being able to increase the version.
BOOST_CLASS_IMPLEMENTATION( freicore::myrimatch::Spectrum, boost::serialization::object_serializable )

// eliminate object tracking at the risk of a programming error creating duplicate objects.
BOOST_CLASS_TRACKING( freicore::myrimatch::Spectrum, boost::serialization::track_never )

#endif
