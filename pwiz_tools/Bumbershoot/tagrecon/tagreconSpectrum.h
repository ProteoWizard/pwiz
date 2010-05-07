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
			ar & intenClass;
		}

		int		intenClass;

	};

	/**!
		SearchResult data structure is a sub-class of BaseSearchResult.
		The data structure is extended to store additional scores like
		mvh, massError, mzFidelity, expectation value and fdr etc.
	*/
	struct SearchResult : public BaseSearchResult
	{
		SearchResult() : BaseSearchResult() {}
		SearchResult( const DigestedPeptide& peptide ) : BaseSearchResult(peptide) {}

		double mvh;
        double mzFidelity;
        double rankScore;

		double mzSSE;
        double massError;

        double XCorr;

		double pvalue;
		double expect;
		double fdr;

        double discriminantScore;
        double probabilisticScore;

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
			scoreList.push_back( SearchScoreInfo( "mvh", mvh ) );
			scoreList.push_back( SearchScoreInfo( "massError", massError ) );
			scoreList.push_back( SearchScoreInfo( "mzSSE", mzSSE ) );
            scoreList.push_back( SearchScoreInfo( "mzFidelity", mzFidelity ) );
            scoreList.push_back( SearchScoreInfo( "XCorr", XCorr ) );
			if( g_rtConfig->CalculateRelativeScores )
			{
				scoreList.push_back( SearchScoreInfo( "pvalue", pvalue ) );
				scoreList.push_back( SearchScoreInfo( "expect", expect ) );
			}
            if( g_rtConfig->PercolatorReranking )
                scoreList.push_back( SearchScoreInfo( "F-score", discriminantScore ) );
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

        /* This function computes the f-score from a set of features. The weights of these features 
           are determined by the ScoreDiscriminant data structure. 
        */
        void computediscriminantScore(const map<string,double>& featureWeights, int zState, MinMaxScoresByZState minScoresByZState, MinMaxScoresByZState maxScoresByZState)
        {
            discriminantScore = 0.0;
            map<string,double>::const_iterator iter;
            // Z-state normalize all the scores
            iter = featureWeights.find("mvh");
            if(iter != featureWeights.end())
                discriminantScore += normalizeScore("mvh", mvh, zState, minScoresByZState, maxScoresByZState) * iter->second;
            iter = featureWeights.find("mzfidelity");
            if(iter != featureWeights.end())
                discriminantScore += normalizeScore("mzfidelity", mzFidelity, zState, minScoresByZState, maxScoresByZState)  * iter->second;
            iter = featureWeights.find("xcorr");
            if(iter != featureWeights.end())
                discriminantScore += normalizeScore("xcorr", XCorr, zState, minScoresByZState, maxScoresByZState)  * iter->second;
            iter = featureWeights.find("numPTMs");
            if(iter!=featureWeights.end())
                discriminantScore += numberOfOtherMods * iter->second;
            iter = featureWeights.find("numBlindPTMs");
            if(iter!=featureWeights.end())
                discriminantScore += numberOfBlindMods * iter->second;
            iter = featureWeights.find("NET");
            if(iter != featureWeights.end())
                discriminantScore += specificTermini() * iter->second;
            iter = featureWeights.find("numMissedCleavs");
            if(iter != featureWeights.end())
                discriminantScore += missedCleavages() * iter->second;
        }

		/*** 
			Operator to sort the search scores based on total scores (MVH)
			This function sorts search results based on mvh, sequence and 
			followed by modification location in that sequence. 
		*/
		bool operator< ( const SearchResult& rhs ) const
		{
			if( rankScore == rhs.rankScore ) {
				return (static_cast<const Peptide&>(*this)) < (static_cast<const Peptide&>(rhs));
			} else {
				return rankScore < rhs.rankScore;
			}
		}

		/// Operator to compare the equality of two search scores (MVH)
		bool operator== ( const SearchResult& rhs ) const
		{
			return ( rankScore == rhs.rankScore && comparePWIZPeptides(static_cast <const Peptide&> (*this), 
														  static_cast<const Peptide&>(rhs)));
		}

		template< class Archive >
		void serialize( Archive& ar, const unsigned int version )
		{
			ar & boost::serialization::base_object< BaseSearchResult >( *this );
			ar & mvh & massError & mzSSE & mzFidelity & rankScore & XCorr & probabilisticScore & discriminantScore;
            ar & numberOfBlindMods & numberOfOtherMods;
			if( g_rtConfig->CalculateRelativeScores )
				ar & pvalue & expect;
		}
	};

	/**!
		Spectrum extends the PeakSpectrum, SearchSpectrum, and TaggingSpectrum
	*/
	struct Spectrum : public PeakSpectrum<PeakInfo>, SearchSpectrum<SearchResult>, TaggingSpectrum
	{
		Spectrum()
			:	BaseSpectrum(), PeakSpectrum<PeakInfo>(), SearchSpectrum<SearchResult>(), TaggingSpectrum()
		{
			resultSet.max_size( g_rtConfig->MaxResults );
            topTargetHits.max_size( 2 );
            topDecoyHits.max_size( 2 );
            //resultSet.max_size( g_rtConfig->maxResultsForInternalUse );
			simplethread_create_mutex( &mutex );
		}

		Spectrum( const Spectrum& old )
			:	BaseSpectrum( old ), PeakSpectrum<PeakInfo>( old ), SearchSpectrum<SearchResult>( old ), TaggingSpectrum( old )
		{
			resultSet.max_size( g_rtConfig->MaxResults );
            topTargetHits.max_size( 2 );
            topDecoyHits.max_size( 2 );
            //resultSet.max_size( g_rtConfig->maxResultsForInternalUse );
			simplethread_create_mutex( &mutex );
		}

		void initialize( int numIntenClasses, int numMzFidelityClasses )
		{
			intenClassCounts.resize( numIntenClasses, 0 );
            mzFidelityThresholds.resize( numMzFidelityClasses, 0 );
		}

		~Spectrum()
		{
			simplethread_destroy_mutex( &mutex );
		}

        void Preprocess();
        void PreprocessForXCorr();

		/**
			ClassifyPeakIntensities function classifies peaks based on 
			intensity. The number of classes are user defined and the
			cardinality of each class is also user defined. Please see
			MyriMatch publication (Journal of Proteome Research; 2007;
			6; 654-661) for further details about the classification
			schema.
		*/
		void ClassifyPeakIntensities()
		{
			// Sort peaks by intensity.
			// Use multimap because multiple peaks can have the same intensity.
			typedef multimap< float, float > IntenSortedPeakPreData;
			IntenSortedPeakPreData intenSortedPeakPreData;
			for( PeakPreData::iterator itr = peakPreData.begin(); itr != peakPreData.end(); ++itr )
			{
				IntenSortedPeakPreData::iterator iItr = intenSortedPeakPreData.insert( pair< float, float >( itr->second, itr->second ) );
				iItr->second = itr->first;
			}

			// Restore the sorting order to be based on MZ
			IntenSortedPeakPreData::reverse_iterator r_iItr = intenSortedPeakPreData.rbegin();
            //cout << id.index << ":" << peakPreData.size() << ":" << intenSortedPeakPreData.size() << endl;
			peakPreData.clear();
			peakData.clear();

			// For each intensity class
			for( int i=0; i < g_rtConfig->NumIntensityClasses; ++i )
			{
				// Determine total number of fragments that can be fitted into the class
				int numFragments = (int) round( (float) ( pow( (float) g_rtConfig->ClassSizeMultiplier, i ) * intenSortedPeakPreData.size() ) / (float) g_rtConfig->minIntensityClassCount, 0 );
				//cout << numFragments << endl;
				// Step through the peaks and assign them the intensity class they belong to
				for( int j=0; r_iItr != intenSortedPeakPreData.rend() && j < numFragments; ++j, ++r_iItr )
				{
					float mz = r_iItr->second;
					float inten = r_iItr->first;
					peakPreData[ mz ] = inten;
					peakData[ mz ].intenClass = i+1;
					if(filteredPeaks.size() < 50) {
						filteredPeaks.insert(multimap<float,float>::value_type(mz, inten));
					}
				}
			}
            //cout << id.index << ":" << peakData.size() << endl;
			intenSortedPeakPreData.clear();

		}

        /* This function predicts the theoretical spectrum for a result, and computes the
            cross-correlation between the predicted spectrum and the experimental spectrum
        */
        void ComputeXCorr(const SearchResult& result, size_t maxIonCharge)
        {
            if(peakDataForXCorr.size()==0)
                return;
            
            // Get the expected width of the array
            int peakDataLength = peakDataForXCorr.size();
            float binWidth = 1.0007079;
            vector<float> theoreticalSpectrum;
            theoreticalSpectrum.resize(peakDataLength);
            fill(theoreticalSpectrum.begin(), theoreticalSpectrum.end(), 0);
            
            // For each peptide bond and charge state
            // Assign an intensity of 50 to b and y ions. 
            // Also assign an intensity of 25 to bins 
            // bordering the b and y ions. Neutral loss
            // and a ions are assigned an intensity of 10.
            Fragmentation fragmentation = result.fragmentation(true, true);
            for(size_t charge = 1; charge <= maxIonCharge; ++charge)
            {
                for(size_t fragIndex = 0; fragIndex < result.sequence().length(); ++fragIndex)
                {
                    size_t nLength = fragIndex;
                    size_t cLength = result.sequence().length() - fragIndex;
                    if(nLength > 0)
                    {
                        // B-ion
                        float fragMass = fragmentation.b(nLength, charge);
                        int bin = (int) (fragMass / binWidth + 0.5f);
                        if( !IS_VALID_INDEX( bin, peakDataLength ) )
                            continue;
                        theoreticalSpectrum[bin] = 50;
                        // Fill the neighbouring bins
                        if( IS_VALID_INDEX( (bin-1), peakDataLength ) )
                            theoreticalSpectrum[bin-1] = 25;
                        if( IS_VALID_INDEX( (bin+1), peakDataLength ) )
                            theoreticalSpectrum[bin+1] = 25;
                        // Neutral loss peaks
                        int NH3LossIndex = (int) ( ( fragMass - (AMMONIA_MONO / charge) ) / binWidth + 0.5f );
                        if( IS_VALID_INDEX( NH3LossIndex, peakDataLength ) )
                            theoreticalSpectrum[NH3LossIndex] = 10;

                        int H20LossIndex = (int)( ( fragMass - (WATER_MONO / charge) ) / binWidth + 0.5f );
                        if ( IS_VALID_INDEX( H20LossIndex, peakDataLength ) )
                            theoreticalSpectrum[H20LossIndex] = 10;
                        // A-ion
                        fragMass = fragmentation.a(nLength, charge);
                        bin = (int) (fragMass / binWidth + 0.5f);
                        if( IS_VALID_INDEX( bin,peakDataLength ) )
                            theoreticalSpectrum[bin] = 10;
                    }
                    if(cLength > 0)
                    {
                        // Y-ion
                        float fragMass = fragmentation.y(cLength, charge);
                        int bin = (int) (fragMass / binWidth + 0.5f);
                        if( !IS_VALID_INDEX( bin, peakDataLength ) )
                            continue;
                        theoreticalSpectrum[bin] = 50;
                        // Fill the neighbouring bins
                        if( IS_VALID_INDEX( (bin-1), peakDataLength ) )
                            theoreticalSpectrum[bin-1] = 25;
                        if( IS_VALID_INDEX( (bin+1), peakDataLength ) )
                            theoreticalSpectrum[bin+1] = 25;
                        // Neutral loss
                        int NH3LossIndex = (int) ( ( fragMass - (AMMONIA_MONO / charge) ) / binWidth + 0.5f );
                        if( IS_VALID_INDEX( NH3LossIndex, peakDataLength ) )
                            theoreticalSpectrum[NH3LossIndex] = 10;
                    }
                }
            }
            
            double rawXCorr = 0.0;
            for(int index = 0; index <  peakDataLength; ++index)
                rawXCorr += peakDataForXCorr[index] * theoreticalSpectrum[index];
            (const_cast<Spectrum::SearchResultType&>(result)).XCorr = (rawXCorr / 1e4);
        }

		/**!
			ScoreSequenceVsSpectrum takes a peptide sequence, predicted sequence ions and experimental spectrum
			to generate MVH and m/z fidelity scores.
		*/
		inline size_t ScoreSequenceVsSpectrum( SearchResult& result, const string& seq, const vector<double>& seqIons, 
                                               int NTT, size_t numDynamicMods, size_t numUnknownMods)
		{
			PeakData::iterator peakItr;
			// Holds the number of occurences of each class
			// of mzFidelity
            MvIntKey mzFidelityKey;

			result.key.clear();
			result.key.resize( g_rtConfig->NumIntensityClasses+1, 0 );
            mzFidelityKey.resize( g_rtConfig->NumMzFidelityClasses+1, 0 );
			// Compute the intensity based MVH and m/z Fidelity scores.
			result.mvh = 0.0;
			result.mzSSE = 0.0;
            result.mzFidelity = 0.0;
            result.numberOfBlindMods = numUnknownMods;
            result.numberOfOtherMods = numDynamicMods;
			size_t numPeaksFound = 0;

			START_PROFILER(6);
			int totalPeaks = (int) seqIons.size();
			
			// For each of the sequence ions
			for( size_t j=0; j < seqIons.size(); ++j )
			{
				// skip theoretical ions outside the scan range of the spectrum
				if( seqIons[j] < mzLowerBound ||
					seqIons[j] > mzUpperBound )
				{
					--totalPeaks; // one less ion to consider because it's out of the scan range
					continue;
				}

				START_PROFILER(7);
				// Find a peak near it
				peakItr = peakData.findNear( seqIons[j], g_rtConfig->FragmentMzTolerance );
				STOP_PROFILER(7);

				// If a peak was found, increment the sequenceInstance's ion correlation triplet
				if( peakItr != peakData.end() )
				{
					numPeaksFound++;
                    double mzError = peakItr->first - seqIons[j];
					result.key.incrementClass( peakItr->second.intenClass-1 );
					result.mzSSE += pow( mzError, 2.0 );
                    mzFidelityKey.incrementClass( ClassifyError( fabs( mzError ), mzFidelityThresholds ) );
				} else
				{
					result.key.incrementClass( g_rtConfig->NumIntensityClasses );
					result.mzSSE += pow( 2.0 * g_rtConfig->FragmentMzTolerance, 2.0 );
                    mzFidelityKey.incrementClass( g_rtConfig->NumMzFidelityClasses );
				}
			}
			STOP_PROFILER(6);

			// Compute squared error of the mass deviations
			result.mzSSE /= totalPeaks;

			double mvh = 0.0;

			START_PROFILER(8);
			
			// Compute the intensity based MVH score and mzFidelity score
			if( result.key.back() != totalPeaks )
			{
				// Total occurences
				int keySum = accumulate( result.key.begin(), result.key.end(), 0 );
				//int numHits = accumulate( intenClassCounts.begin(), intenClassCounts.end()-1, 0 );
				// Total number of empty classes
				int numVoids = intenClassCounts.back();
				//int totalPeakBins = numVoids + peakCount;
                int totalPeakBins = numVoids + peakData.size();

				// Compute the MVH for intensity class
                for( size_t i=0; i < intenClassCounts.size(); ++i ) {
					mvh += lnCombin( intenClassCounts[i], result.key[i] );
                }
				
				mvh -= lnCombin( totalPeakBins, keySum );
                result.mvh = -mvh;
				
				// Variables to compute the mzFidelity class based MVH score
                int N;
			    double sum1 = 0, sum2 = 0;
			    int numHits = accumulate( result.key.begin(), result.key.end(), 0 );
                int totalPeakSpace = numVoids + numHits;
			    double pHits = (double) numHits / (double) totalPeakSpace;
			    double pMisses = 1.0 - pHits;

				// Total number of mzFidelity classes
                N = accumulate( mzFidelityKey.begin(), mzFidelityKey.end(), 0 );
				int p = 0;

				// For each mzFidelity class
				for( int i=0; i < g_rtConfig->NumMzFidelityClasses; ++i )
				{
					// This value is always equal to 1??
					p = 1 << i;
					double pKey = pHits * ( (double) p / (double) g_rtConfig->minMzFidelityClassCount );
					// Compute the sub-score of MVH
					sum1 += log( pow( pKey, mzFidelityKey[i] ) );
					sum2 += g_lnFactorialTable[ mzFidelityKey[i] ];
				}
				// Compute the sub-score for the misses
				sum1 += log( pow( pMisses, mzFidelityKey.back() ) );
                sum2 += g_lnFactorialTable[ mzFidelityKey.back() ];
				// Compute the total score
                result.mzFidelity = -1.0 * double( ( g_lnFactorialTable[ N ] - sum2 ) + sum1 );

                // Penalize the mvh based on the peptide enzymatic status
                // We take out 10% of the score for every non-conforming termini
                //double penalizedMVH = result.mvh - (result.mvh * 0.1 * (2-NTT));
                //result.rankScore = g_rtConfig->UseNETAdjustment ? penalizedMVH : result.mvh;

                // Reward the MVH of the peptide according to its enzymatic status
                double rewardedMVH = result.mvh + -1.0 * g_rtConfig->NETRewardVector[NTT];
                result.probabilisticScore = g_rtConfig->UseNETAdjustment ? rewardedMVH : result.mvh;
                // Penalize the score for number of modifications
                if(g_rtConfig->PenalizeUnknownMods)
                {
                    double modPenalty = 0.0;
                    modPenalty = numDynamicMods * result.probabilisticScore * 0.025 + result.probabilisticScore * numUnknownMods * 0.05;
                    result.probabilisticScore -= modPenalty;
                }
                result.rankScore = result.probabilisticScore;
			}
			STOP_PROFILER(8);
			return numPeaksFound;
		}

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

		vector< int >		intenClassCounts;
        vector< double >	mzFidelityThresholds;
        FragmentTypesBitset fragmentTypes;
		
        vector< float >		mOfPrecursorList;

		/**!
			CalculateMvhProbabilities_R enumerates all possible class combinations
			for a particular class and computes the MVH probability of each combination
			of a class.
		*/
        void CalculateMvhProbabilities_R(	const int minValue,
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
				//	p += lnCombin( classCounts[i], key[i], lnFT );
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
		void CalculateMvhProbabilities(	const int minValue /* minimum value of all classes */,
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

		void CalculateRelativeScores()
		{
			map< size_t, PValueByMvhProbability > pValueByMvhProbabilityCache;
			for( SearchResultSetType::iterator rItr = resultSet.begin(); rItr != resultSet.end(); ++rItr )
			{
				int fragmentsPredicted = accumulate( rItr->key.begin(), rItr->key.end(), 0 );

				PValueByMvhProbability& pValueByMvhProbability = pValueByMvhProbabilityCache[ fragmentsPredicted ];
				if( pValueByMvhProbability.empty() )
				{
					CalculateMvhProbabilities(	0, fragmentsPredicted, g_rtConfig->NumIntensityClasses+1,
												intenClassCounts, pValueByMvhProbability, g_lnFactorialTable );
					//cout << pValueByMvhProbability[ rItr->mvh ] << endl;
					//cout << pValueByMvhProbability << endl;
				}

				PValueByMvhProbability::iterator pValueItr = pValueByMvhProbability.lower_bound( rItr->mvh );
				double pvalue = ( pValueItr == pValueByMvhProbability.end() ? pValueByMvhProbability.rbegin()->second : pValueItr->second );
				const_cast< SearchResult& >( *rItr ).pvalue = (float) pvalue;
				const_cast< SearchResult& >( *rItr ).expect = (float) ( pvalue * (numTargetComparisons+numDecoyComparisons) );
			}
		}

		Histogram< float >	scoreHistogram;
		map< float, int > scores;

		simplethread_mutex_t mutex;
	};

	struct SpectraList : public	PeakSpectraList< Spectrum, SpectraList >,
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
