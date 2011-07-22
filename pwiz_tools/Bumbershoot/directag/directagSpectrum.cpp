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
// The Original Code is the DirecTag peptide sequence tagger.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Zeqiang Ma
//

#include "stdafx.h"
#include "directagSpectrum.h"

using namespace freicore;

namespace std
{
	ostream& operator<< ( ostream& o, const freicore::directag::PeakInfo& rhs )
	{
		return o << "( " << rhs.intensityRank << " )";
	}

	ostream& operator<< ( ostream& o, const freicore::directag::GapInfo& rhs )
	{
		return o << "(GapInfo: " << rhs.fromPeakItr->first << " " << rhs.peakItr->first << " " << rhs.gapMass << " " << rhs.gapRes << " " << rhs.error << " )";
	}

	ostream& operator<< ( ostream& o, const freicore::directag::gapVector_t& rhs )
	{
		o << "(GapVector:";
		for( freicore::directag::gapVector_t::const_iterator itr = rhs.begin(); itr != rhs.end(); ++itr )
			 o << " " << *itr;
		o << " )";

		return o;
	}

	ostream& operator<< ( ostream& o, const freicore::directag::gapMap_t& rhs )
	{
		o << "(GapMap:";
		for( freicore::directag::gapMap_t::const_iterator itr = rhs.begin(); itr != rhs.end(); ++itr )
			o << " " << itr->first << "->" << itr->second << "\n";
		o << " )";

		return o;
	}
}

namespace freicore
{
namespace directag
{
	MzFEBins SpectraList::mzFidelityErrorBins;
	CEBinsList SpectraList::complementErrorBinsList;
	IRBinsTable SpectraList::intensityRanksumBinsTable;

	Spectrum::Spectrum()
		:	BaseSpectrum(), PeakSpectrum< PeakInfo >(), TaggingSpectrum()
	{
		tagList.max_size( g_rtConfig->MaxTagCount );

		scoreWeights[ "intensity" ] = intensityScoreWeight = g_rtConfig->IntensityScoreWeight;
		scoreWeights[ "mzFidelity" ] = mzFidelityScoreWeight = g_rtConfig->MzFidelityScoreWeight;
		scoreWeights[ "complement" ] = complementScoreWeight = g_rtConfig->ComplementScoreWeight;
		scoreWeights[ "random" ] = g_rtConfig->RandomScoreWeight;
	}

	Spectrum::Spectrum( const Spectrum& old )
		:	BaseSpectrum( old ), PeakSpectrum< PeakInfo >( old ), TaggingSpectrum( old )
	{
		scoreWeights[ "intensity" ] = intensityScoreWeight = g_rtConfig->IntensityScoreWeight;
		scoreWeights[ "mzFidelity" ] = mzFidelityScoreWeight = g_rtConfig->MzFidelityScoreWeight;
		scoreWeights[ "complement" ] = complementScoreWeight = g_rtConfig->ComplementScoreWeight;
		scoreWeights[ "random" ] = g_rtConfig->RandomScoreWeight;
	}

	void Spectrum::ClassifyPeakIntensities()
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
		peakPreData.clear();
		peakData.clear();

		for( int i=0; i < g_rtConfig->NumIntensityClasses; ++i )
		{
			int numFragments = (int) round( (double) ( pow( (double) g_rtConfig->ClassSizeMultiplier, i ) * intenSortedPeakPreData.size() ) / (double) g_rtConfig->minIntensityClassCount, 0 );
			for( int j=0; r_iItr != intenSortedPeakPreData.rend() && j < numFragments; ++j, ++r_iItr )
			{
				double mz = r_iItr->second;
				double inten = r_iItr->first;
				peakPreData.insert( peakPreData.end(), make_pair( mz, inten ) );
				peakData[ mz ].intensityRank = peakPreData.size();
				peakData[ mz ].intensity = inten;
			}
		}
		intenSortedPeakPreData.clear();
	}

	// Attempts to find a complement for each peak in the spectrum
	// Returns the sum of products of the found complements' intensities
	double Spectrum::FindComplements( double complementMzTolerance )
	{
		double sumOfProducts = 0;
        complementaryTIC = 0;
		for( PeakPreData::iterator itr = peakPreData.begin(); itr != peakPreData.end(); ++itr )
		{
			PeakInfo& peak = peakData[ itr->first ];// = PeakInfo();

			for( int z=0; z < numFragmentChargeStates; ++z )
			{
				double complementMz = CalculateComplementMz( itr->first, z+1 );
				PeakPreData::iterator complementItr = peakPreData.findNear( complementMz, complementMzTolerance, true );
				if( complementItr != peakPreData.end() )
				{
					sumOfProducts += itr->second * complementItr->second;
                    complementaryTIC += itr->second;
					peak.hasComplementAsCharge[z] = true;
				} else
					peak.hasComplementAsCharge[z] = false;
			}
		}

		return sumOfProducts;
	}

	size_t Spectrum::MakeTagGraph()
	{
		PeakData::iterator left;	// main iterator pointing to the first peak in a comparison
		PeakData::iterator cur;		// main iterator pointing to the peak currently being looked at
		m2n_t::const_iterator resItr;
		size_t numResidueMassGaps = 0;

		gapMaps.clear();
		tagGraphs.clear();
		nodeSet.clear();

		gapMaps.resize( numFragmentChargeStates );
		tagGraphs.resize( numFragmentChargeStates );

		for( int z=0; z < numFragmentChargeStates; ++z )
		{
			gapMap_t& gapMap = gapMaps[z];
			spectrumGraph& tagGraph = tagGraphs[z];

			for( left = peakData.begin(); left != peakData.end(); ++left )
			{
				for( resItr = g_residueMap->beginMonoMasses(); resItr != g_residueMap->endMonoMasses(); ++resItr )
				{
					if( resItr->second == PEPTIDE_N_TERMINUS_SYMBOL || resItr->second == PEPTIDE_C_TERMINUS_SYMBOL )
						continue;

					double mzGap = resItr->first / (float) (z+1);
					double expectedMZ = left->first + mzGap;
					cur = peakData.findNear( expectedMZ, g_rtConfig->FragmentMzTolerance );

					if( cur != peakData.end() )
					{
						// Calculate the error between the m/z of the actual peak and the m/z that was expected for it
						double error = (cur->first - left->first) - mzGap;
						if( fabs( error ) > g_rtConfig->FragmentMzTolerance )
							continue;

						gapMap_t::iterator nextGapInfo = gapMap.insert( gapMap_t::value_type( cur->first, gapVector_t() ) ).first;
						gapMap[ left->first ].push_back( GapInfo( left, cur, nextGapInfo, mzGap, resItr->second, error ) );
						++ numResidueMassGaps;

						GapInfo newEdge( left, cur, nextGapInfo, mzGap, resItr->second, error, left->first, cur->first );
						tagGraph[ left->first ].cEdges.push_back( newEdge );
						tagGraph[ cur->first ].nEdges.push_back( newEdge );
						tagGraph[ cur->first ].nPathSize = max(	tagGraph[ cur->first ].nPathSize,
																tagGraph[ left->first ].nPathSize + 1 );
						nodeSet.insert( left->first );
						nodeSet.insert( cur->first );
					}
				}
			}

			for( spectrumGraph::reverse_iterator itr = tagGraph.rbegin(); itr != tagGraph.rend(); ++itr )
			{
				for( size_t i=0; i < itr->second.nEdges.size(); ++i )
				{
					tagGraph[ itr->second.nEdges[i].nTermMz ].cPathSize = max(	tagGraph[ itr->second.nEdges[i].nTermMz ].cPathSize,
																				itr->second.cPathSize + 1 );
				}

				itr->second.longestPath = itr->second.cPathSize + itr->second.nPathSize;
			}
		}

        tagGraphPeakCount = nodeSet.size();
        tagGraphTIC = 0;
        for( nodeSet_t::iterator itr = nodeSet.begin(); itr != nodeSet.end(); ++itr )
            tagGraphTIC += peakPreData[*itr];

		return numResidueMassGaps;
	}

	void Spectrum::FilterPeaks()
	{
		// Secondly, determine the neutral mass of the precursor (m/z * z - z)
		mOfPrecursor = mzOfPrecursor * id.charge - ( id.charge * PROTON );

		numFragmentChargeStates = max( 1, id.charge - 1 );

		if( peakPreData.empty() )
			return;

		// Eliminate peaks above the precursor's mass with a given tolerance
		double maxPeakMass = mOfPrecursor + PROTON + g_rtConfig->PrecursorMzTolerance;
		PeakPreData::iterator itr = peakPreData.upper_bound( maxPeakMass );
		peakPreData.erase( itr, peakPreData.end() );

		if( peakPreData.empty() )
			return;

		// Thirdly, store the bounds of the spectrum before eliminating any peaks
		mzLowerBound = peakPreData.begin()->first;
		mzUpperBound = peakPreData.rbegin()->first;
		totalPeakSpace = mzUpperBound - mzLowerBound;

		if( g_rtConfig->MakeSpectrumGraphs )
			writeToSvgFile( "-unprocessed" + g_rtConfig->OutputSuffix );

		if( g_rtConfig->DeisotopingMode > 0 || g_rtConfig->AdjustPrecursorMass )
		{
			Deisotope( g_rtConfig->IsotopeMzTolerance );

			if( g_rtConfig->MakeSpectrumGraphs )
				writeToSvgFile( "-deisotoped" + g_rtConfig->OutputSuffix );
		}

		FilterByTIC( g_rtConfig->TicCutoffPercentage );
		FilterByPeakCount( g_rtConfig->MaxPeakCount );

		if( g_rtConfig->MakeSpectrumGraphs )
			writeToSvgFile( "-filtered" + g_rtConfig->OutputSuffix );

		if( peakPreData.size() < (size_t) g_rtConfig->minIntensityClassCount )
		{
			peakPreData.clear();
			return;
		}

		// Create peak data from pre peak data
		/*ClassifyPeakIntensities();

		MakeTagGraph();
		//map< int, int > pathSizeHistogram;
		int maxLongestPath = 0;
		map< float, int > longestPathMap;

		for( int z=0; z < numFragmentChargeStates; ++z )
		{
			spectrumGraph& tagGraph = tagGraphs[z];
			for( spectrumGraph::reverse_iterator itr = tagGraph.rbegin(); itr != tagGraph.rend(); ++itr )
			{
				if( longestPathMap[ itr->first ] < itr->second.longestPath )
					longestPathMap[ itr->first ] = itr->second.longestPath;

				if( itr->second.longestPath > maxLongestPath )
					maxLongestPath = itr->second.longestPath;

				//++ pathSizeHistogram[ itr->second.longestPath ];
			}
			deallocate(tagGraph);
		}
		//cout << id << " peak path histogram:\n" << pathSizeHistogram << endl;

		vector< PeakData::iterator > junkPeaks;
		for( PeakData::iterator cur = peakData.begin(); cur != peakData.end(); ++cur )
		{
			if( longestPathMap[ cur->first ] < g_rtConfig->tagPeakCount )
				junkPeaks.push_back( cur );
		}

		for( size_t i=0; i < junkPeaks.size(); ++i )
		{
			peakData.erase( junkPeaks[i] );
			peakPreData.erase( junkPeaks[i]->first );
		}

		if( peakData.size() < (size_t) g_rtConfig->minIntensityClassCount )
		{
			deallocate(peakPreData);
			deallocate(peakData);
			return;
		}*/

		// Create peak data from pre peak data
		ClassifyPeakIntensities();

		for( PeakData::iterator itr = peakData.begin(); itr != peakData.end(); ++itr ) 
		{
			itr->second.hasComplementAsCharge.resize(numFragmentChargeStates, false);
		}

		totalPeakSpace = peakData.rbegin()->first - peakData.begin()->first;
		peakCount = (int) peakData.size();
	}

	void Spectrum::Preprocess()
	{
		PeakPreData::iterator itr;
		PeakPreData::reverse_iterator r_itr;
		PeakPreData::iterator findItr;

		if( mzOfPrecursor < 1 )
		{
			peakPreData.clear();
			return;
		}

		if( g_rtConfig->AdjustPrecursorMass )
		{
			double originalPrecursorMass = mOfPrecursor;
			double originalPrecursorMz = mzOfPrecursor;
			double bestPrecursorAdjustment = 0.0;
			double maxSumOfProducts = 0.0;
			map<double, double> AdjustmentResults;

			for( mOfPrecursor += g_rtConfig->MinPrecursorAdjustment;
				 mOfPrecursor <= originalPrecursorMass + g_rtConfig->MaxPrecursorAdjustment;
				 mOfPrecursor += g_rtConfig->PrecursorAdjustmentStep )
			{
				mzOfPrecursor = ( mOfPrecursor + ( id.charge * PROTON ) ) / id.charge;

				double sumOfProducts = FindComplements( g_rtConfig->ComplementMzTolerance );

				if( sumOfProducts > maxSumOfProducts )
				{
					maxSumOfProducts = sumOfProducts;
					bestPrecursorAdjustment = mOfPrecursor - originalPrecursorMass;
				}

				AdjustmentResults[ mOfPrecursor ] = sumOfProducts;
			}

			if( maxSumOfProducts > 0.0 )
			{
				mOfPrecursor = originalPrecursorMass + bestPrecursorAdjustment;
				mzOfPrecursor = ( mOfPrecursor + ( id.charge * PROTON ) ) / id.charge;
			} else
			{
				mOfPrecursor = originalPrecursorMass;
				mzOfPrecursor = originalPrecursorMz;
			}

			if( g_rtConfig->MakeSpectrumGraphs )
			{
				writeToSvgFile( "-adjusted" + g_rtConfig->OutputSuffix );
				cout << "Original precursor m/z: " << originalPrecursorMz << endl;
				cout << "Corrected precursor m/z: " << mzOfPrecursor << endl;
				cout << "Sum of complement products: " << maxSumOfProducts << endl;

				/*cout << "Best complement total: " << BestComplementTotal << endl;
				cout << oldPrecursor << " (" << spectrum->mOfPrecursorFixed << ") corrected by " << spectrum->mzOfPrecursor - oldPrecursor <<
						" to " << spectrum->mzOfPrecursor << " (" << spectrum->mOfPrecursor << ") " << endl;*/

				cout << AdjustmentResults << endl;
			}
		}

		// Initialize the spectrum info tables
		initialize( g_rtConfig->NumIntensityClasses, g_rtConfig->NumMzFidelityClasses );

		if( peakData.size() < (size_t) g_rtConfig->minIntensityClassCount )
		{
			peakPreData.clear();
			peakData.clear();
			return;
		}

		// Reclassify intensities and find complements based on fully processed spectrum
		ClassifyPeakIntensities();
				
		for( PeakData::iterator itr = peakData.begin(); itr != peakData.end(); ++itr )
           {
                 itr->second.hasComplementAsCharge.resize(numFragmentChargeStates, false);
           }

		FindComplements( g_rtConfig->ComplementMzTolerance );

		totalPeakSpace = peakData.rbegin()->first - peakData.begin()->first;
		peakCount = (int) peakData.size();
	}

	void Spectrum::MakeProbabilityTables()
	{
		for( PeakData::iterator itr = peakData.begin(); itr != peakData.end(); ++itr )
		{
			itr->second.hasSomeComplement = accumulate( itr->second.hasComplementAsCharge.begin(),
														itr->second.hasComplementAsCharge.begin() + numFragmentChargeStates,
														0 );
			++ complementClassCounts[ ( itr->second.hasSomeComplement == 0 ? 1 : 0 ) ];
		}

		if( complementClassCounts[0] == 0 )
		{
			//scoreWeights["complement"] = 0;
			complementScoreWeight = 0;
		} else
			CreateScoringTableMVH( 0, g_rtConfig->tagPeakCount, 2, complementClassCounts, bgComplements, g_lnFactorialTable, false, false, false );
	}

	size_t Spectrum::Score()
	{
		START_PROFILER(3)
		size_t numTagsGenerated = findTags();
		STOP_PROFILER(3)

		// compute approximate tagMzRange for ScanRanker
		float tagMzRangeLowerBound = peakData.rbegin()->first;
		float tagMzRangeUpperBound = peakData.begin()->first;

		for( TagList::iterator itr = interimTagList.begin(); itr != interimTagList.end(); ++itr )
		{
			TagInfo& tag = const_cast< TagInfo& >( *itr );
			//tag.CalculateTotal( scoreWeights );
			START_PROFILER(4)
			tag.CalculateTotal( complementScoreWeight, intensityScoreWeight, mzFidelityScoreWeight );
			STOP_PROFILER(4)
			tag.totalScore *= numTagsGenerated;
			//for( map< string, double >::iterator scoreItr = tag.scores.begin(); scoreItr != tag.scores.end(); ++scoreItr )
			//	scoreItr->second *= numTagsGenerated;

			tagMzRangeLowerBound = min( tagMzRangeLowerBound, tag.lowPeakMz);
			tagMzRangeUpperBound = max( tagMzRangeUpperBound, tag.highPeakMz);

			START_PROFILER(5)
			if( g_rtConfig->MaxTagScore == 0 || tag.totalScore <= g_rtConfig->MaxTagScore )
				tagList.insert( tag );
			STOP_PROFILER(5)
		}

		tagMzRange = ((tagMzRangeUpperBound - tagMzRangeLowerBound) < 0) ? 0 : (tagMzRangeUpperBound - tagMzRangeLowerBound);

		// Code for ScanRanker
		bestTagScore = (tagList.empty()) ? 0 : tagList.rbegin()->chisquared;
		bestTagTIC = (tagList.empty()) ? 0 : tagList.rbegin()->tagTIC;

		START_PROFILER(6)
		deallocate( interimTagList );

		deallocate( bgComplements );
		STOP_PROFILER(6)

		return numTagsGenerated;
	}

	// Takes a tag and recursively fills a list of strings of variations of that tag based on I/L substitutions
	void TagExploder_R( const string& tag, int idx, vector< string >& tagList )
	{
		if( idx == (int) tag.length() )
		{
			tagList.push_back( tag );
			return;
		}

		if( tag[idx] == 'I' )
		{
			string newTag( tag );
			newTag[idx] = 'L';
			TagExploder_R( newTag, idx+1, tagList );
		}

		TagExploder_R( tag, idx+1, tagList );
	}

	void TagExploder( const string& tag, vector< string >& tagList )
	{
		tagList.push_back( tag );
		TagExploder_R( tag, 0, tagList );
	}

	void Spectrum::findTags_R(	gapMap_t::iterator gapInfoItr,
								int tagIndex,
								string& tag,
								vector< double >& peakErrors,
								vector< PeakData::iterator >& peakList,
								int peakChargeState,
								size_t& numTagsGenerated,
								IRBins& irBins )
	{
		if( tagIndex == 0 )
		{
			++ numTagsGenerated;

			TagInfo newTag;

			MvIntKey intensityClassKey, complementClassKey, mzFidelityKey;
			intensityClassKey.resize( g_rtConfig->NumIntensityClasses, 0 );
			complementClassKey.resize( 2, 0 );

			vector<double> modelPeaks;
			vector<double> modelErrors;
			vector<double> modelSquaredErrors;

			double gapMass = 0.0;
			modelPeaks.push_back( peakList[0]->first );
			for( int i=0; i < g_rtConfig->TagLength; ++i )
			{
				gapMass += g_residueMap->getMonoMassByName( tag[i] ) / (double) peakChargeState;
				modelPeaks.push_back( peakList[i+1]->first - gapMass );
			}
			double sum = accumulate( modelPeaks.begin(), modelPeaks.end(), 0.0 );
			double avg = sum / g_rtConfig->tagPeakCount;

			sum = 0.0;
			for( int i=0; i < g_rtConfig->tagPeakCount; ++i )
			{
				modelErrors.push_back( fabs( modelPeaks[i] - avg ) );
				sum += pow( modelErrors[i], 2 );
				//cout << e1 << " " << e2 << " " << e3 << ": " << errors << endl;
			}

			MzFEBins::iterator binItr = SpectraList::mzFidelityErrorBins.upper_bound( sum );
			-- binItr;
			//newTag.scores[ "mzFidelity" ] = binItr->second;
			newTag.mzFidelityScore = binItr->second;

			gapMass = 0.0;
			modelPeaks[0] = avg;
			for( int i=0; i < g_rtConfig->TagLength; ++i )
			{
				gapMass += g_residueMap->getMonoMassByName( tag[i] ) / (double) peakChargeState;
				modelPeaks[i+1] = avg + gapMass;
			}
			//cout << peakList << " " << modelPeaks << " " << modelErrors << endl;

			//int totalPathLength = 0;
			int totalIntensityRanks = 1;
			double totalIntensity = 0;
			//int totalContextRanks = 1;
			vector< double > complementPairMasses;
			//spectrumGraph& tagGraph = tagGraphs[peakChargeState-1];
			for( int i=0; i < g_rtConfig->tagPeakCount; ++i )
			{
				PeakInfo& peak = peakList[i]->second;

				newTag.worstPeakRank = max( peak.intensityRank, newTag.worstPeakRank );
				totalIntensityRanks += peak.intensityRank;
				totalIntensity += peak.intensity;

				bool hasComplement = peak.hasComplementAsCharge[ peakChargeState-1 ];
				++ complementClassKey[ hasComplement ? 0 : 1 ];
				if( hasComplement )
				{
					double complementMz = CalculateComplementMz( peakList[i]->first, peakChargeState );
					PeakData::iterator complementItr = peakData.findNear( complementMz, g_rtConfig->ComplementMzTolerance );
					complementPairMasses.push_back( peakList[i]->first + complementItr->first );
				}
			}

			//newTag.scores[ "intensity" ] = irBins[ totalIntensityRanks ];
			newTag.intensityScore = irBins[ totalIntensityRanks ];
			newTag.ranksum = totalIntensityRanks;
			newTag.tagTIC = (float) totalIntensity;

			double complementClassScore = 0;
			if( complementClassCounts[0] > 0 )
			{
				CEBins::iterator binItr = SpectraList::complementErrorBinsList[2].begin();
				if( complementClassKey[0] > 1 )
				{
					double complementPairMean = arithmetic_mean<double>( complementPairMasses );
					for( size_t i=0; i < complementPairMasses.size(); ++i )
						complementPairMasses[i] = pow( complementPairMasses[i] - complementPairMean, 2.0 );
					double sse = accumulate( complementPairMasses.begin(), complementPairMasses.end(), 0.0 );

					binItr = SpectraList::complementErrorBinsList[complementClassKey[0]].upper_bound( sse );
					-- binItr;
					while(binItr->second == 0)
						++ binItr;
				}
				MvhTable::reverse_iterator itr;
				int i = g_rtConfig->tagPeakCount;
				for( itr = bgComplements.rbegin(); itr != bgComplements.rend() && i >= (int) complementPairMasses.size(); ++itr, --i )
					complementClassScore += (double) exp(itr->second);
				--itr;
				if( i >= 1 )
					complementClassScore -= (double) exp(itr->second) * ( 1.0 - binItr->second );
				else
					complementClassScore -= (double) exp(itr->second) / 2.0;
				//newTag.scores[ "complement" ] = complementClassScore;
				newTag.complementScore = complementClassScore;
				//cout << id.index << ": " << complementClassKey << " " << complementClassCounts << " " << complementClassScore << " " << complementPairMasses << " " << binItr->second << " " << i << " " << itr->second << endl;
			} else
				//newTag.scores[ "complement" ] = 1.0;
				newTag.complementScore = 1.0;

			//newNode.peakList = peakList;

			if( g_rtConfig->RandomScoreWeight != 0 )
				newTag.scores[ "random" ] = (double) g_rtConfig->GetRandomScore();

			newTag.lowPeakMz = peakList.front()->first;
			newTag.highPeakMz = peakList.back()->first;

			//----------------------------------------- lower y - water+proton 
			//newNode->cTerminusMass = max( 0.0, *peakList.begin() - WATER + PROTON );
			newTag.cTerminusMass = modelPeaks.front() * peakChargeState - ( PROTON * peakChargeState );
			newTag.cTerminusMass = max( 0.0, newTag.cTerminusMass - WATER_MONO );

			//---------------------------- neutral precursor - proton ----- upper y
			//newNode->nTerminusMass = max( 0.0, mOfPrecursor + 1 - *peakList.rbegin() );
			newTag.nTerminusMass = modelPeaks.back() * peakChargeState - ( PROTON * peakChargeState );
			newTag.nTerminusMass = max( 0.0, mOfPrecursor - newTag.nTerminusMass );

			string properTag = tag;
			std::reverse( properTag.begin(), properTag.end() );

			newTag.tag = properTag;
			newTag.totalScore = (double) tagCount;
			newTag.chargeState = peakChargeState;

			++ tagCount;
			interimTagList.insert( newTag );
		} else
		{
			if( gapInfoItr == gapMaps[peakChargeState-1].end() )
				return;

			gapVector_t& gapVector = gapInfoItr->second;

			if( gapVector.empty() )
				return;

			peakList.push_back( gapVector.front().fromPeakItr );

			size_t gapCount = gapVector.size();
			for( size_t i=0; i < gapCount; ++i )
			{
				if( tagIndex-1 == 0 )
					peakList.push_back( gapVector[i].peakItr );

				tag.push_back( gapVector[i].gapRes );
				peakErrors.push_back( gapVector[i].error );
				findTags_R( gapVector[i].nextGapInfo,
							tagIndex-1,
							tag,
							peakErrors,
							peakList,
							peakChargeState,
							numTagsGenerated,
							irBins );
				peakErrors.pop_back();
				tag.erase( tag.length()-1 );

				if( tagIndex-1 == 0 )
					peakList.pop_back();
			}

			peakList.pop_back();
		}
	}

	size_t Spectrum::findTags()
	{
		size_t numTagsGenerated = 0;
		gapMap_t::iterator gapInfoItr;
		string tag;
		vector< double > peakErrors;
		vector< PeakData::iterator > peakList;
		IRBins& irBins = SpectraList::intensityRanksumBinsTable[ g_rtConfig->TagLength ][ peakData.size() ];
//cout << peakData.size() << irBins << endl;
		peakErrors.reserve( g_rtConfig->tagPeakCount );
		peakList.reserve( g_rtConfig->tagPeakCount );

		tagCount = 0;

		for( int z=0; z < numFragmentChargeStates; ++z )
		{
			gapMap_t& gapMap = gapMaps[z];
			for( gapInfoItr = gapMap.begin(); gapInfoItr != gapMap.end(); ++gapInfoItr )
				findTags_R( gapInfoItr, g_rtConfig->TagLength, tag, peakErrors, peakList, z+1, numTagsGenerated, irBins );
		}

		return numTagsGenerated;
	}

	void SpectraList::CalculateIRBins_R( IRBins& theseRanksumBins, int tagLength, int numPeaks, int curRanksum, int curRank, int loopDepth )
	{
		if( loopDepth > tagLength )
			++ theseRanksumBins[ curRanksum ];
		else
			for( int rank = curRank + 1; rank <= numPeaks; ++rank )
				CalculateIRBins_R( theseRanksumBins, tagLength, numPeaks, curRanksum + rank, rank, loopDepth+1 );
	}

	void SpectraList::CalculateIRBins( int tagLength, int numPeaks )
	{
		if( intensityRanksumBinsTable.size() <= (size_t) tagLength )
			intensityRanksumBinsTable.resize( tagLength+1, vector< IRBins >() );
		if( intensityRanksumBinsTable[ tagLength ].size() <= (size_t) numPeaks )
			intensityRanksumBinsTable[ tagLength ].resize( numPeaks+1, IRBins() );
		IRBins& theseRanksumBins = intensityRanksumBinsTable[ tagLength ][ numPeaks ];
		theseRanksumBins.resize( (tagLength+1) * numPeaks, 0 );
		CalculateIRBins_R( theseRanksumBins, tagLength, numPeaks, 0, 0, 0 );

		double totalRanksum = 0;
		for( IRBins::iterator itr = theseRanksumBins.begin(); itr != theseRanksumBins.end(); ++itr )
			totalRanksum += *itr;

		double tmpRanksum = 0;
		for( IRBins::iterator itr = theseRanksumBins.begin(); itr != theseRanksumBins.end(); ++itr )
		{
			tmpRanksum += *itr;
			*itr = tmpRanksum / totalRanksum;
		}
	}

	void SpectraList::PrecacheIRBins( SpectraList& instance )
	{
		intensityRanksumBinsTable.clear();

        if( g_pid == 0 )
		    cout << "Reading intensity ranksum bins cache file." << endl;
		ifstream cacheInputFile( "directag_intensity_ranksum_bins.cache" );
		if( cacheInputFile.is_open() )
		{
			text_iarchive cacheInputArchive( cacheInputFile );
			cacheInputArchive & intensityRanksumBinsTable;
		}
		cacheInputFile.close();

		if( g_pid == 0 )
		    cout << "Calculating uncached ranksum bins (this could take a while)." << endl;
		for( iterator itr = instance.begin(); itr != instance.end(); ++itr )
		{
			if( intensityRanksumBinsTable.size() <= (size_t) g_rtConfig->TagLength ||
				intensityRanksumBinsTable[ g_rtConfig->TagLength ].size() <= (*itr)->peakData.size() ||
				intensityRanksumBinsTable[ g_rtConfig->TagLength ][ (*itr)->peakData.size() ].empty() )
			{
				//cout << g_rtConfig->TagLength << "," << (*itr)->peakData.size() << endl;
				CalculateIRBins( g_rtConfig->TagLength, (*itr)->peakData.size() );
			}
		}

		if( g_pid == 0 )
		{
			cout << "Writing intensity ranksum bins cache file." << endl;
			ofstream cacheOutputFile( "directag_intensity_ranksum_bins.cache" );
			text_oarchive cacheOutputArchive( cacheOutputFile );
			cacheOutputArchive & intensityRanksumBinsTable;
			cacheOutputFile.close();
		}
	}

	void SpectraList::InitMzFEBins()
	{
		int numPeaks = g_rtConfig->tagPeakCount;
		vector< double > peakErrors( numPeaks );
		double peakErrorSum = 0.0;
		for( int i=0; i < numPeaks; ++i )
		{
			peakErrors[i] = g_rtConfig->FragmentMzTolerance * i;
			peakErrorSum += peakErrors[i];
		}

		double peakErrorAvg = peakErrorSum / numPeaks;
		for( int i=0; i < numPeaks; ++i )
			peakErrors[i] -= peakErrorAvg;
		//cout << peakErrors << endl;

		double maxError = 0.0;
		for( int i=0; i < numPeaks; ++i )
			maxError += pow( peakErrors[i], 2 );
		//cout << maxError << endl;

		mzFidelityErrorBins[ 0.0 ] = 0.0;

		peakErrors.clear();
		peakErrors.resize( numPeaks, 0.0 );
		vector< double > sumErrors( numPeaks, 0.0 );
		vector< double > adjustedSumErrors( numPeaks, 0.0 );

		// Random sampling permits longer tag lengths
		boost::mt19937 rng(0);
		boost::uniform_real<double> MzErrorRange( -g_rtConfig->FragmentMzTolerance, g_rtConfig->FragmentMzTolerance );
		boost::variate_generator< boost::mt19937&, boost::uniform_real<double> > RandomMzError( rng, MzErrorRange );
		for( int i=0; i < g_rtConfig->MzFidelityErrorBinsSamples; ++i )
		{
			for( int p=1; p < numPeaks; ++p )
			{
				double e = RandomMzError();
				peakErrors[p] = e;
				sumErrors[p] = accumulate( peakErrors.begin(), peakErrors.begin()+p, e );
			}
			//cout << sumErrors << endl;
			//double sum = accumulate( peakErrors.begin(), peakErrors.end(), 0.0 );
			//double avg = sum / (int) peakErrors.size();
			double sum = accumulate( sumErrors.begin(), sumErrors.end(), 0.0 );
			double avg = sum / (int) sumErrors.size();

			sum = 0.0;
			for( size_t i=0; i < sumErrors.size(); ++i )
			{
				adjustedSumErrors[i] = sumErrors[i] - avg;
				sum += pow( adjustedSumErrors[i], 2 );
			}

			mzFidelityErrorBins[ sum ] = 0;
		}

		double n = 0.0;
		double totalSize = (double) mzFidelityErrorBins.size();
		for( MzFEBins::iterator itr = mzFidelityErrorBins.begin(); itr != mzFidelityErrorBins.end(); ++itr )
		{
			n += 1.0;
			itr->second = n / totalSize;
		}
		//cout << mzFidelityErrorBins << endl;
	}

	void SpectraList::InitCEBins()
	{
		boost::mt19937 rng(0);
		boost::uniform_real<double> ComplementErrorRange( -g_rtConfig->ComplementMzTolerance, g_rtConfig->ComplementMzTolerance );
		boost::variate_generator< boost::mt19937&, boost::uniform_real<double> > RandomComplementError( rng, ComplementErrorRange );
		complementErrorBinsList.resize( g_rtConfig->tagPeakCount+1, CEBins() );
		for( int numComplements = 2; numComplements <= g_rtConfig->tagPeakCount; ++numComplements )
		{
			CEBins& errorBins = complementErrorBinsList[numComplements];
			errorBins[0.0] = 0.0;
			for( int i=0; i < g_rtConfig->MzFidelityErrorBinsSamples; ++i )
			{
				vector< double > errors;
				for( int j=0; j < numComplements; ++j )
					errors.push_back( RandomComplementError() );
				double mean = arithmetic_mean<float>(errors);
				for( int j=0; j < numComplements; ++j )
					errors[j] = pow( errors[j] - mean, 2.0 );
				double sse = accumulate( errors.begin(), errors.end(), 0.0 );
				errorBins[sse] = 0;
			}
			double count = 0;
			for( map< double, double >::iterator itr = errorBins.begin(); itr != errorBins.end(); ++itr, ++count )
				itr->second = count / (double) errorBins.size();
			//cout << errorBins << endl << endl;
		}
	}
}
}
