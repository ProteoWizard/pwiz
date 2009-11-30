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

#ifndef _MYRIMATCHSPECTRUM_H
#define _MYRIMATCHSPECTRUM_H

#include "stdafx.h"
#include "freicore.h"
#include "myrimatchConfig.h"
#include "SearchSpectrum.h"
#include "PeakSpectrum.h"
#include "simplethreads.h"
#include "Histogram.h"
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
			ar & intenClass;
		}

		int		intenClass;

	};

	typedef BasePeakData< PeakInfo > PeakData;

	struct SearchResult : public BaseSearchResult
	{
		SearchResult() : BaseSearchResult() {}
		SearchResult( const DigestedPeptide& c ) : BaseSearchResult( c ) {}

		double mvh;
		double massError;
		double mzSSE;
        double mzFidelity;
		double pvalue;
		double expect;
		double fdr;
		double newMZFidelity;
		// Mean absolute error
		double mzMAE;
		
		double deltaMVHAvg;
		double deltaMVHMode;
		double deltaMVHSeqType;
		double deltaMVHSmartSeqType;
		
		double deltaMZFidelityAvg;
		double deltaMZFidelityMode;
		double deltaMZFidelitySeqType;
		double deltaMZFidelitySmartSeqType;

		double mvhMode;
		double mzFidelityMode;

        vector<double> matchedIons;

		double getTotalScore() const
		{
			return mvh;
		}

		SearchScoreList getScoreList() const
		{
			SearchScoreList scoreList;
			scoreList.push_back( SearchScoreInfo( "mvh", mvh ) );
			scoreList.push_back( SearchScoreInfo( "massError", massError ) );
			scoreList.push_back( SearchScoreInfo( "mzSSE", mzSSE ) );
            scoreList.push_back( SearchScoreInfo( "mzFidelity", mzFidelity ) );
			scoreList.push_back( SearchScoreInfo( "newMZFidelity" , newMZFidelity ) );
			scoreList.push_back( SearchScoreInfo( "mzMAE", mzMAE ) ); 
			if( g_rtConfig->CalculateRelativeScores )
			{
				scoreList.push_back( SearchScoreInfo( "pvalue", pvalue ) );
				scoreList.push_back( SearchScoreInfo( "expect", expect ) );
			}
			
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

		/*** 
			Operator to sort the search scores based on total scores (MVH)
			This function sorts search results based on mvh, sequence and 
			followed by modification location in that sequence. 
		*/
		bool operator< ( const SearchResult& rhs ) const
		{
			if( mvh == rhs.mvh ) {
				return (static_cast<const Peptide&>(*this)) < (static_cast<const Peptide&>(rhs));
			} else {
				return mvh < rhs.mvh;
			}
		}

		/// Operator to compare the equality of two search scores (MVH)
		bool operator== ( const SearchResult& rhs ) const
		{
			return ( mvh == rhs.mvh && comparePWIZPeptides(static_cast <const Peptide&> (*this), 
														  static_cast<const Peptide&>(rhs)));
		}

		template< class Archive >
		void serialize( Archive& ar, const unsigned int version )
		{
			ar & boost::serialization::base_object< BaseSearchResult >( *this );
			ar & mvh & massError & mzSSE & mzFidelity & newMZFidelity & mzMAE;
			ar & deltaMVHAvg & deltaMVHMode & deltaMVHSeqType & deltaMVHSmartSeqType;
			ar & deltaMZFidelityMode & deltaMZFidelityAvg & deltaMZFidelitySeqType & deltaMZFidelitySmartSeqType;
			ar & mvhMode & mzFidelityMode;
			if( g_rtConfig->CalculateRelativeScores )
				ar & pvalue & expect;
		}
	};

	struct Spectrum : public PeakSpectrum< PeakInfo >, SearchSpectrum< SearchResult >
	{
		Spectrum()
			:	BaseSpectrum(), PeakSpectrum< PeakInfo >(), SearchSpectrum< SearchResult >(),
			scoreHistogram( g_rtConfig->NumScoreHistogramBins, g_rtConfig->MaxScoreHistogramValues )
		{
			resultSet.max_size( g_rtConfig->MaxResults );
			simplethread_create_mutex( &mutex );
		}

		Spectrum( const Spectrum& old )
			:	BaseSpectrum( old ), PeakSpectrum< PeakInfo >( old ), SearchSpectrum< SearchResult >( old ),
			scoreHistogram( g_rtConfig->NumScoreHistogramBins, g_rtConfig->MaxScoreHistogramValues )
		{
			resultSet.max_size( g_rtConfig->MaxResults );
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
		int QuerySequence();

		void ClassifyPeakIntensities()
		{
			// Sort peaks by intensity.
			// Use multimap because multiple peaks can have the same intensity.
			typedef multimap< double, double > IntenSortedPeakPreData;
			IntenSortedPeakPreData intenSortedPeakPreData;
			for( PeakPreData::iterator itr = peakPreData.begin(); itr != peakPreData.end(); ++itr )
			{
				IntenSortedPeakPreData::iterator iItr = intenSortedPeakPreData.insert( make_pair( itr->second, itr->second ) );
				iItr->second = itr->first;
			}

			// Restore the sorting order to be based on MZ
			IntenSortedPeakPreData::reverse_iterator r_iItr = intenSortedPeakPreData.rbegin();
            //cout << id.index << peakPreData.size() << endl;
			peakPreData.clear();
			peakData.clear();

			for( int i=0; i < g_rtConfig->NumIntensityClasses; ++i )
			{
				int numFragments = (int) round( (double) ( pow( (double) g_rtConfig->ClassSizeMultiplier, i ) * intenSortedPeakPreData.size() ) / (double) g_rtConfig->minIntensityClassCount, 0 );
				//cout << numFragments << endl;
				for( int j=0; r_iItr != intenSortedPeakPreData.rend() && j < numFragments; ++j, ++r_iItr )
				{
					double mz = r_iItr->second;
					double inten = r_iItr->first;
					peakPreData[ mz ] = inten;
					peakData[ mz ].intenClass = i+1;
				}
			}
			intenSortedPeakPreData.clear();
		}

		inline void ScoreSequenceVsSpectrum( SearchResult& result, const string& seq, const vector< double >& seqIons )
		{
			PeakData::iterator peakItr;
			MvIntKey mzFidelityKey;

			result.key.clear();
			result.key.resize( g_rtConfig->NumIntensityClasses+1, 0 );
			mzFidelityKey.resize( g_rtConfig->NumMzFidelityClasses+1, 0 );
			result.mvh = 0.0;
			result.mzSSE = 0.0;
			result.mzFidelity = 0.0;
			result.newMZFidelity = 0.0;
			result.mzMAE = 0.0;
			result.matchedIons.clear();


			START_PROFILER(6);
			int totalPeaks = (int) seqIons.size();

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
				// Find the fragment ion peak. Consider the fragment ion charge state while setting the
				// mass window for the fragment ion lookup.
				double massError = g_rtConfig->fragmentMzToleranceUnits == PPM ? (seqIons[j] * g_rtConfig->FragmentMzTolerance * pow(10.0,-6)) : g_rtConfig->FragmentMzTolerance;
				peakItr = peakData.findNear( seqIons[j], massError );
				STOP_PROFILER(7);

				// If a peak was found, increment the sequenceInstance's ion correlation triplet
				if( peakItr != peakData.end() )
				{
					double mzError = peakItr->first - seqIons[j];
					// Convert the mass error appropriately
					if(g_rtConfig->fragmentMzToleranceUnits == PPM)
						mzError = (mzError/seqIons[j])*pow(10.0,6);
					result.key.incrementClass( peakItr->second.intenClass-1 );
					result.mzSSE += pow( mzError, 2.0 );
					result.mzMAE += fabs(mzError);
					mzFidelityKey.incrementClass( ClassifyError( fabs( mzError ), mzFidelityThresholds ) );
					int mzFidelityClass = ClassifyError( fabs( mzError ), g_rtConfig->massErrors );
					result.newMZFidelity += g_rtConfig->mzFidelityLods[mzFidelityClass];
					//result.matchedIons.push_back(peakItr->first);
				} else
				{
					result.key.incrementClass( g_rtConfig->NumIntensityClasses );
					result.mzSSE += pow( 2.0 * g_rtConfig->FragmentMzTolerance, 2.0 );
					result.mzMAE += 2.0 * g_rtConfig->FragmentMzTolerance;
					mzFidelityKey.incrementClass( g_rtConfig->NumMzFidelityClasses );
				}

			}
			STOP_PROFILER(6);

			result.mzSSE /= totalPeaks;
			result.mzMAE /= totalPeaks;
			// Convert the new mzFidelity score into normal domain.
			result.newMZFidelity = exp(result.newMZFidelity);

			double mvh = 0.0;

			START_PROFILER(8);
			if( result.key.back() != totalPeaks )
			{
				int keySum = accumulate( result.key.begin(), result.key.end(), 0 );
				//int numHits = accumulate( intenClassCounts.begin(), intenClassCounts.end()-1, 0 );
				int numVoids = intenClassCounts.back();
				int totalPeakBins = numVoids + peakCount;

				for( size_t i=0; i < intenClassCounts.size(); ++i ) {
					mvh += lnCombin( intenClassCounts[i], result.key[i] );
                    /*if(result.sequence().compare("HVGDLGNVTADK")==0) {
                        cout << "IC["<< i << "]:"<< intenClassCounts[i] << "," << result.key[i] << endl;
                    }*/
				}
				mvh -= lnCombin( totalPeakBins, keySum );
                /*if(result.sequence().compare("HVGDLGNVTADK")==0) {
                    cout << numVoids << "," << totalPeakBins << "," << peakCount << "," << keySum <<endl;
                }*/

				result.mvh = -mvh;


				int N;
				double sum1 = 0, sum2 = 0;
				int numHits = accumulate( result.key.begin(), result.key.end(), 0 );
				int totalPeakSpace = numVoids + numHits;
				double pHits = (double) numHits / (double) totalPeakSpace;
				double pMisses = 1.0 - pHits;

				N = accumulate( mzFidelityKey.begin(), mzFidelityKey.end(), 0 );
				int p = 0;

				//cout << id << ": " << mzFidelityKey << endl;

				//if( id == 2347 ) cout << pHits << " " << totalPeakSpace << " " << peakData.size() << endl;
				for( int i=0; i < g_rtConfig->NumMzFidelityClasses; ++i )
				{
					p = 1 << i;
					double pKey = pHits * ( (double) p / (double) g_rtConfig->minMzFidelityClassCount );
					//if( id == 2347 ) cout << " " << pKey << " " << mzFidelityKey[i] << endl;
					sum1 += log( pow( pKey, mzFidelityKey[i] ) );
					sum2 += g_lnFactorialTable[ mzFidelityKey[i] ];
				}
				sum1 += log( pow( pMisses, mzFidelityKey.back() ) );
				sum2 += g_lnFactorialTable[ mzFidelityKey.back() ];
				result.mzFidelity = -1.0 * double( ( g_lnFactorialTable[ N ] - sum2 ) + sum1 );
			}
			
			STOP_PROFILER(8);
		}

		template< class Archive >
		void serialize( Archive& ar, const unsigned int version )
		{
			ar & boost::serialization::base_object< BaseSpectrum >( *this );
			ar & boost::serialization::base_object< PeakSpectrum< PeakInfo > >( *this );
			ar & boost::serialization::base_object< SearchSpectrum< SearchResult > >( *this );

			ar & intenClassCounts;
            ar & mzFidelityThresholds;
            ar & fragmentTypes;
			ar & mOfPrecursorList;
			if( g_rtConfig->MakeScoreHistograms )
				ar & scoreHistogram;

			ar & mvhScoreDistribution;
			ar & mzFidelityDistribution;
		}

		vector< int >		intenClassCounts;
        vector< double >		mzFidelityThresholds;
		vector< double>			newMZFidelityThresholds;
        FragmentTypesBitset fragmentTypes;
		
		vector< double >	mOfPrecursorList;

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
				double lnP = 0.0;
				for( int i=0; i < totalClasses; ++i )
					lnP += lnCombin( classCounts[i], key[i], lnFT );
				//float p = 0.0;
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

		typedef map< double, double > PValueByMvhProbability;
		void CalculateMvhProbabilities(	const int minValue,
										const int totalValue,
										const int numClasses,
										vector< int > classCounts,
										PValueByMvhProbability& pValueByMvhProbability,
										lnFactorialTable& lnFT )
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
			CalculateMvhProbabilities_R( minValue, totalValue, numClasses, classCounts, mvhProbabilities, key, lnFT );
			STOP_PROFILER(10);

			std::sort( mvhProbabilities.begin(), mvhProbabilities.end() );
			//cout << mvhProbabilities << endl;
			double pSum = exp( mvhProbabilities[0] );
			for( size_t i=1; i < mvhProbabilities.size(); ++i )
			{
				double& curProb = mvhProbabilities[i];
				if( mvhProbabilities[i-1] < curProb )
				{
					pSum += exp( curProb );
					pValueByMvhProbability[ - (double) curProb ] = pSum;
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
				const_cast< SearchResult& >( *rItr ).pvalue = (double) pvalue;
				const_cast< SearchResult& >( *rItr ).expect = (double) ( pvalue * (numTargetComparisons+numDecoyComparisons) );
			}
		}

		Histogram< double >	scoreHistogram;
		map< double, int > scores;

		
		// Keep track of the score distributions
		map<int, int> mvhScoreDistribution;
		map<int, int> mzFidelityDistribution;
		
		void computeSecondaryScores() {

			//Compute the average and the mode of the MVH and mzFidelity distrubutions
			double averageMVHValue = 0.0;
			double totalComps = 0.0;
			int maxValue = INT_MIN;
			for(map<int,int>::iterator itr = mvhScoreDistribution.begin(); itr!= mvhScoreDistribution.end(); ++itr) {
				if((*itr).first==0) {
					continue;
				}
				// Sum the score distribution
				averageMVHValue += ((*itr).second * (*itr).first);
				totalComps += (*itr).second;
				// Get the max value
				maxValue = maxValue < (*itr).second ? (*itr).second : maxValue;
			}
			// Compute the average
			averageMVHValue /= totalComps;

			// Locate the most frequent mvh score
			double mvhMode;
			for(map<int,int>::iterator itr = mvhScoreDistribution.begin(); itr!= mvhScoreDistribution.end(); ++itr) {
				if((*itr).second==maxValue) {
					mvhMode = (double) (*itr).first;
					break;
				}
			}
			
			
			// Compute the average and mode of the mzFidelity score distrubtion just like the mvh score 
			// distribution
			double averageMZFidelity = 0.0;
			totalComps = 0.0;
			maxValue = INT_MIN;
			for(map<int,int>::iterator itr = mzFidelityDistribution.begin(); itr!= mzFidelityDistribution.end(); ++itr) {
				if((*itr).first==0) {
					continue;
				}
				averageMZFidelity += ((*itr).second * (*itr).first);
				totalComps += (*itr).second;
				maxValue = maxValue < (*itr).second ? (*itr).second : maxValue;
			}
			averageMZFidelity /= totalComps;

			double mzFidelityMode;
			for(map<int,int>::iterator itr = mzFidelityDistribution.begin(); itr!= mzFidelityDistribution.end(); ++itr) {
				if((*itr).second==maxValue) {
					mzFidelityMode = (double) (*itr).first;
					break;
				}
			}
			double massError = 2.5;
			// For each search result
			for( SearchResultSetType::const_reverse_iterator rItr = resultSet.rbegin(); rItr != resultSet.rend(); ++rItr )
			{
				// Init the default values for all delta scores.
				const_cast< SearchResult& >( *rItr ).deltaMVHSeqType = 0.0;
				const_cast< SearchResult& >( *rItr ).deltaMVHSmartSeqType = 0.0;
				const_cast< SearchResult& >( *rItr ).deltaMVHMode = -1.0;
				const_cast< SearchResult& >( *rItr ).deltaMZFidelitySeqType = 0.0;
				const_cast< SearchResult& >( *rItr ).deltaMZFidelitySmartSeqType = 0.0;
				const_cast< SearchResult& >( *rItr ).deltaMZFidelityMode = -1.0;
				const_cast< SearchResult& >( *rItr ).deltaMVHAvg = -1.0;
				const_cast< SearchResult& >( *rItr ).deltaMZFidelityAvg = -1.0;
				const_cast < SearchResult& >(*rItr ).mvhMode = 0.0;
				const_cast < SearchResult& >(*rItr ).mzFidelityMode = 0.0;

				// Check to see if the mvh and mzFidelity scores are above zero.
				bool validMVHScore = rItr->mvh > 0.0 ? true : false;
				bool validMZFidelityScore = rItr->mzFidelity > 0.0 ? true : false;

				// If the mvh score is valid then compute the deltaMVH using the (thisMVH-averageMVH)/thisMVH
				// Also compute the deltaMVH using (thisMVH-modeMVH)/thisMVH.
				if(validMVHScore) {			
					const_cast< SearchResult& >( *rItr ).deltaMVHAvg = (rItr->mvh-averageMVHValue);
					const_cast< SearchResult& >( *rItr ).deltaMVHMode = (rItr->mvh-mvhMode);
					const_cast < SearchResult& >(*rItr ).mvhMode = mvhMode;
				}
				// Compute the deltaMZFidelity values just like the deltaMVH values described above.
				if(validMZFidelityScore) {
					const_cast< SearchResult& >( *rItr ).deltaMZFidelityAvg = (rItr->mzFidelity-averageMZFidelity);
					const_cast< SearchResult& >( *rItr ).deltaMZFidelityMode = (rItr->mzFidelity-mzFidelityMode);
				}
				
				
				// Compute the smart sequest type deltaMVH value as (thisMVH-nextBestMVH)/thisMVH
				// nextBestMVH is the next lowest MVH that matches to a different sequence. Please
				// note that this treats the peptide sequences that match with same MVH score as
				// same. It also treats a peptide sequence with ambiguous interpretations
				// as same.
				SearchResultSetType::const_reverse_iterator reverIter = rItr;
				// Get the current sequence
				string currentPep = rItr->sequence();
				// Go down the list of results and locate the next best result that doesn't have the
				// same MVH score and the same peptide sequence.
				//while(validMVHScore && reverIter!=resultSet.begin() && (currentPep.compare(reverIter->sequence())==0 || rItr->mvh==reverIter->mvh)) {
				while(validMVHScore && reverIter!=resultSet.rend() && (isIsobaric(static_cast< const DigestedPeptide& >(*rItr),static_cast< const DigestedPeptide& >(*reverIter),massError)==true || rItr->mvh==reverIter->mvh)) {
					++reverIter;
				}
				// Compute the deltaMVH using the located result
				if(reverIter!=resultSet.rend() && validMVHScore && (currentPep.compare(reverIter->sequence())!=0 && rItr->mvh!=reverIter->mvh) && reverIter->mvh > 0.0) {
					const_cast< SearchResult& >( *rItr ).deltaMVHSmartSeqType = (rItr->mvh-reverIter->mvh);
				}

				// Compute the sequest type deltaMVH as (thisMVH-nextBestMVH)/thisMVH.
				// nextBestMVH in this context is the next lowest MVH value regardless of the
				// sequence it matched to. Please note that this treats peptides with ambiguous
				// modification interpretations as different entities.
				reverIter = rItr;
				// Locate the next result with the lowest score
				while(validMVHScore && reverIter!=resultSet.rend() && rItr->mvh==reverIter->mvh) {
					++reverIter;
				}
				// Compute the deltaMVH
				if(reverIter!=resultSet.rend() && validMVHScore && rItr->mvh!=reverIter->mvh && reverIter->mvh > 0.0) {
					const_cast< SearchResult& >( *rItr ).deltaMVHSeqType = (rItr->mvh-reverIter->mvh);
				}

				// Compute the smart sequest type deltaMZFidelity value as (thisMZFidelity-nextBestMZFidelity)/thisMZFidelity
				// nextBestMZFidelity is the next lowest MZFidelity that matches to a different sequence. Please
				// note that this treats the peptide sequences that match with same MZFidelity score as
				// same. It also treats a peptide sequence with ambiguous interpretations
				// as same.
				reverIter = rItr;
				// Go down the list of results and locate the next best result that doesn't have the
				// same MVH score and the same peptide sequence.
				while(validMZFidelityScore && reverIter!=resultSet.rend() && (isIsobaric(static_cast< const DigestedPeptide& >(*rItr),static_cast< const DigestedPeptide& >(*reverIter),massError)==true || rItr->mzFidelity==reverIter->mzFidelity)) {
					++reverIter;
				}
				// Compute the deltaMVH using the located result
				if(reverIter!=resultSet.rend() && validMZFidelityScore && (currentPep.compare(reverIter->sequence())!=0 && rItr->mzFidelity!=reverIter->mzFidelity) && reverIter->mzFidelity > 0.0) {
					const_cast< SearchResult& >( *rItr ).deltaMZFidelitySmartSeqType = (rItr->mzFidelity-reverIter->mzFidelity);
				}

				// Compute the sequest type deltaMZFidelity as (thisMZFidelity-nextBestMZFidelity)/thisMZFidelity.
				// nextBestMZFidelity in this context is the next lowest MZFidelity value regardless of the
				// sequence it matched to. Please note that this treats peptides with ambiguous
				// modification interpretations as different entities.
				reverIter = rItr;
				// Locate the next result with the lowest score
				while(validMZFidelityScore && reverIter!=resultSet.rend() && rItr->mzFidelity==reverIter->mzFidelity) {
					++reverIter;
				}
				// Compute the deltaMZFidelity
				if(reverIter!=resultSet.rend() && validMZFidelityScore && rItr->mzFidelity!=reverIter->mzFidelity && reverIter->mzFidelity > 0.0) {
					const_cast< SearchResult& >( *rItr ).deltaMZFidelitySeqType = (rItr->mzFidelity-reverIter->mzFidelity);
				}
			}
		}
		

		simplethread_mutex_t mutex;
	};

	struct SpectraList : public	PeakSpectraList< Spectrum, SpectraList >,
								SearchSpectraList< Spectrum, SpectraList >
	{
		using BaseSpectraList< Spectrum, SpectraList >::ListIndex;
		using BaseSpectraList< Spectrum, SpectraList >::ListIndexIterator;

		void dump()
		{
			ofstream dumpFile( ((*begin())->id.source + "-dump.txt").c_str(), ios::binary );
			for( const_iterator sItr = begin(); sItr != end(); ++sItr )
			{
				Spectrum* s = *sItr;
				dumpFile << s->id.index << ":" << "fileName\t" << s->fileName << "\n";
				dumpFile << s->id.index << ":" << "sourceName\t" << s->id.source << "\n";
				dumpFile << s->id.index << ":" << "chargeState\t" << s->id.charge << "\n";
				dumpFile << s->id.index << ":" << "peakPreCount\t" << s->peakPreCount << "\n";
				dumpFile << s->id.index << ":" << "peakCount\t" << s->peakCount << "\n";
				dumpFile << s->id.index << ":" << "numTargetComparisons\t" << s->numTargetComparisons << "\n";
                dumpFile << s->id.index << ":" << "numDecoyComparisons\t" << s->numDecoyComparisons << "\n";
				dumpFile << s->id.index << ":" << "numFragmentChargeStates\t" << s->numFragmentChargeStates << "\n";
				dumpFile << s->id.index << ":" << "mzOfPrecursor\t" << s->mzOfPrecursor << "\n";
				dumpFile << s->id.index << ":" << "mOfPrecursor\t" << s->mOfPrecursor << "\n";
				dumpFile << s->id.index << ":" << "mOfUnadjustedPrecursor\t" << s->mOfUnadjustedPrecursor << "\n";
				dumpFile << s->id.index << ":" << "mOfPrecursorList\t" << s->mOfPrecursorList << "\n";
				dumpFile << s->id.index << ":" << "retentionTime\t" << s->retentionTime << "\n";
				dumpFile << s->id.index << ":" << "processingTime\t" << s->processingTime << "\n";
				dumpFile << s->id.index << ":" << "mzUpperBound\t" << s->mzUpperBound << "\n";
				dumpFile << s->id.index << ":" << "mzLowerBound\t" << s->mzLowerBound << "\n";
				dumpFile << s->id.index << ":" << "totalIonCurrent\t" << s->totalIonCurrent << "\n";
				dumpFile << s->id.index << ":" << "totalPeakSpace\t" << s->totalPeakSpace << "\n";
				dumpFile << s->id.index << ":" << "intenClassCounts\t" << s->intenClassCounts << "\n";
			}
		}
	};
}
}

// eliminate serialization overhead at the cost of never being able to increase the version.
BOOST_CLASS_IMPLEMENTATION( freicore::myrimatch::Spectrum, boost::serialization::object_serializable )

// eliminate object tracking at the risk of a programming error creating duplicate objects.
BOOST_CLASS_TRACKING( freicore::myrimatch::Spectrum, boost::serialization::track_never )

#endif
