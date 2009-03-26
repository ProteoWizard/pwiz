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
		SearchResult( const DigestedPeptide& peptide) : BaseSearchResult(peptide) {}

		double mvh;
		double massError;
		double mzSSE;
        double mzFidelity;
		double pvalue;
		double expect;
		double fdr;
        vector<double> matchedIons;

		double getTotalScore() const
		{
			return mvh;
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
			if( g_rtConfig->CalculateRelativeScores )
			{
				scoreList.push_back( SearchScoreInfo( "pvalue", pvalue ) );
				scoreList.push_back( SearchScoreInfo( "expect", expect ) );
			}
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
			ar & mvh & massError & mzSSE & mzFidelity;
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
			simplethread_create_mutex( &mutex );
		}

		Spectrum( const Spectrum& old )
			:	BaseSpectrum( old ), PeakSpectrum<PeakInfo>( old ), SearchSpectrum<SearchResult>( old ), TaggingSpectrum( old )
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

		/**!
			ScoreSequenceVsSpectrum takes a peptide sequence, predicted sequence ions and experimental spectrum
			to generate MVH and m/z fidelity scores.
		*/
		inline size_t ScoreSequenceVsSpectrum( SearchResult& result, const string& seq, const vector<double>& seqIons )
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
                    /*if(result.sequence().compare("HVGDLGNVTADK")==0) {
                        cout << "IC["<< i << "]:"<< intenClassCounts[i] << "," << result.key[i] << endl;
                    }*/
                }
				
				mvh -= lnCombin( totalPeakBins, keySum );
                result.mvh = -mvh;
                /*if(result.sequence().compare("HVGDLGNVTADK")==0) {
                    cout << id.id << "," << numVoids << "," << totalPeakBins << "," << peakCount << "," << keySum <<endl;
                    cout << id.id << "," << peakData.size() << endl;
                }*/
				
				/*cout << id << ": mvh=" << result.mvh << "->Seq=" << result.sequence << " TicCutoffPercentage=" << (g_rtConfig->TicCutoffPercentage)*100.0 << endl;
				cout << "\tintenClassCounts.size()=" << intenClassCounts.size() << endl;
				for(size_t i = 0; i < intenClassCounts.size(); i++) {
					cout << "\t\t(intenClassCounts[" << i << "],result.key[" << i << "])" << intenClassCounts[i] << "," << result.key[i] << endl;
				}
				exit (1);*/


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

				//cout << id << ": " << mzFidelityKey << endl;

				//if( id == 2347 ) cout << pHits << " " << totalPeakSpace << " " << peakData.size() << endl;
				// For each mzFidelity class
				for( int i=0; i < g_rtConfig->NumMzFidelityClasses; ++i )
				{
					// This value is always equal to 1??
					p = 1 << i;
					double pKey = pHits * ( (double) p / (double) g_rtConfig->minMzFidelityClassCount );
					//if( id == 2347 ) cout << " " << pKey << " " << mzFidelityKey[i] << endl;
					// Compute the sub-score of MVH
					sum1 += log( pow( pKey, mzFidelityKey[i] ) );
					sum2 += g_lnFactorialTable[ mzFidelityKey[i] ];
				}
				// Compute the sub-score for the misses
				sum1 += log( pow( pMisses, mzFidelityKey.back() ) );
                sum2 += g_lnFactorialTable[ mzFidelityKey.back() ];
				// Compute the total score
                result.mzFidelity = -1.0 * double( ( g_lnFactorialTable[ N ] - sum2 ) + sum1 );
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
