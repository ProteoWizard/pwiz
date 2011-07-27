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
// The Original Code is the Pepitome search engine.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

#ifndef _PEPITOMESPECTRUM_H
#define _PEPITOMESPECTRUM_H

#include "stdafx.h"
#include "freicore.h"
#include "pepitomeConfig.h"
#include "SearchSpectrum.h"
#include "PeakSpectrum.h"
#include <boost/thread/thread.hpp>
#include <boost/thread/mutex.hpp>
#include "Histogram.h"
#include <bitset>
#include <boost/math/distributions/hypergeometric.hpp>
#include <boost/math/distributions/normal.hpp> // for normal_distribution
using boost::math::normal; // typedef provides default type is double.
using boost::math::hypergeometric_distribution;

namespace freicore
{
namespace pepitome
{
	struct PeakInfo
	{
		template< class Archive >
		void serialize( Archive& ar, const int unsigned version )
		{
			ar & intenClass & rawIntensity & intensityRank & normIntensity;
		}

        int     intenClass;
        float   rawIntensity;
        int     intensityRank;
        float   normIntensity;

        PeakInfo() : intenClass(0), rawIntensity(0.0f), intensityRank(0), normIntensity(0.0f) {}

        PeakInfo(float intensity, int rank = 0, float nInten = 0.0f, int iClass = 0)
        {
            intenClass = 0;
            rawIntensity = intensity;
            intensityRank = rank;
            normIntensity = nInten;
        }

	};
    
    typedef BasePeakData< PeakInfo > PeakData;
	
    struct SearchResult : public BaseSearchResult
	{
		SearchResult() : BaseSearchResult("A") {}
		SearchResult( const DigestedPeptide& c ) : BaseSearchResult( c ) {}

        bool _isDecoy;
        bool isDecoy() const {return _isDecoy;}

		double mvh;
		double massError;
		double mzSSE;
        double mzFidelity;
		double newMZFidelity;
		// Mean absolute error
		double mzMAE;
		
        // Computes the p-value of matching more peaks by random chance
        double hgt;
        // Kendall Tau
        double kendallTau;
        double kendallPVal;
        // Ranking score
        double rankingScore;

        vector<double> matchedIons;

		double getTotalScore() const
		{
			return mvh;
		}

		SearchScoreList getScoreList() const
		{
            SearchScoreList scoreList;
			scoreList.push_back( SearchScore( "mvh", mvh, MS_MyriMatch_MVH ) );
            scoreList.push_back( SearchScore( "mzFidelity", mzFidelity, MS_MyriMatch_mzFidelity ) );

			scoreList.push_back( SearchScore( "massError", massError ) );
			scoreList.push_back( SearchScore( "mzSSE", mzSSE ) );
			scoreList.push_back( SearchScore( "newMZFidelity" , newMZFidelity ) );
			scoreList.push_back( SearchScore( "mzMAE", mzMAE ) );

            scoreList.push_back( SearchScore( "hgt", hgt ) ); 
            scoreList.push_back( SearchScore( "kendallTau", kendallTau ) );
            scoreList.push_back( SearchScore( "kendallPVal", kendallPVal ) );

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

		/*** 
			Operator to sort the search scores based on total scores (MVH)
			This function sorts search results based on mvh, sequence and 
			followed by modification location in that sequence. 
		*/
		bool operator< ( const SearchResult& rhs ) const
		{
			return rankingScore < rhs.rankingScore;
		}

		/// Operator to compare the equality of two search scores (MVH)
		bool operator== ( const SearchResult& rhs ) const
		{
			return rankingScore == rhs.rankingScore;
		}

		template< class Archive >
		void serialize( Archive& ar, const unsigned int version )
		{
			ar & boost::serialization::base_object< BaseSearchResult >( *this );
			ar & mvh & massError & mzSSE & mzFidelity & newMZFidelity & mzMAE & hgt;
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

        void ClassifyPeakIntensities(bool rankPeaks = false, bool DotProduct = false);

        void ScoreSpectrumVsSpectrum( SearchResult& result, const PeakData& libPeaks );

        void computeSecondaryScores();

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

		vector< int >		    intenClassCounts;
        vector< double >		mzFidelityThresholds;
		vector< double>			newMZFidelityThresholds;
        
        Histogram< double >	scoreHistogram;
		map< double, int > scores;
		
		// Keep track of the score distributions
		map<int, int> mvhScoreDistribution;
		map<int, int> mzFidelityDistribution;
        
        boost::mutex mutex;
	};

	struct SpectraList : public	PeakSpectraList< Spectrum, SpectraList >,
								SearchSpectraList< Spectrum, SpectraList >
	{
		using BaseSpectraList< Spectrum, SpectraList >::ListIndex;
		using BaseSpectraList< Spectrum, SpectraList >::ListIndexIterator;
	};
}
}

// eliminate serialization overhead at the cost of never being able to increase the version.
BOOST_CLASS_IMPLEMENTATION( freicore::pepitome::Spectrum, boost::serialization::object_serializable )

// eliminate object tracking at the risk of a programming error creating duplicate objects.
BOOST_CLASS_TRACKING( freicore::pepitome::Spectrum, boost::serialization::track_never )

#endif
