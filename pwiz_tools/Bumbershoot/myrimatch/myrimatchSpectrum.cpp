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

#include "stdafx.h"
#include "myrimatchSpectrum.h"

using namespace freicore;

namespace std
{
	ostream& operator<< ( ostream& o, const freicore::myrimatch::PeakInfo& rhs )
	{
		return o << rhs.normalizedIntensity;
	}
}

namespace freicore
{
namespace myrimatch
{
	void Spectrum::Preprocess()
	{
		PeakPreData::iterator itr;
		PeakPreData::reverse_iterator r_itr;

		if( mzOfPrecursor < 1 )
		{
			peakPreData.clear();
			return;
		}

		if( peakPreData.empty() )
			return;

        // calculate precursor mass hypotheses
        if (precursorMzType == MassType_Monoisotopic || g_rtConfig->precursorMzToleranceRule == MzToleranceRule_Mono)
        {
            // for monoisotopic precursors, create a hypothesis for each adjustment and possible charge state
            IntegerSet::const_iterator itr = g_rtConfig->MonoisotopeAdjustmentSet.begin();
            for (; itr != g_rtConfig->MonoisotopeAdjustmentSet.end(); ++itr)
                BOOST_FOREACH(int charge, possibleChargeStates)
                {
                    PrecursorMassHypothesis p;
                    p.mass = Ion::neutralMass(mzOfPrecursor, charge, 0, *itr);
                    p.massType = MassType_Monoisotopic;
                    p.charge = charge;
                    precursorMassHypotheses.push_back(p);
                }
        }
        else
        {
            // for average precursors, create a hypothesis for each possible charge state
            BOOST_FOREACH(int charge, possibleChargeStates)
            {
                PrecursorMassHypothesis p;
                p.mass = Ion::neutralMass(mzOfPrecursor, charge);
                p.massType = MassType_Average;
                p.charge = charge;
                precursorMassHypotheses.push_back(p);
            }
        }

        // sort hypotheses ascending by mass
        sort(precursorMassHypotheses.begin(), precursorMassHypotheses.end());

        //PeakPreData unfilteredPeakPreData = peakPreData;

        // filter out peaks above the largest precursor hypthesis' mass
        peakPreData.erase( peakPreData.upper_bound( precursorMassHypotheses.back().mass + g_rtConfig->AvgPrecursorMzTolerance ), peakPreData.end() );

		// The old way of calculating these values:
        /*mzLowerBound = peakPreData.begin()->first;
		mzUpperBound = peakPreData.rbegin()->first;
		totalPeakSpace = mzUpperBound - mzLowerBound;*/

		FilterByTIC( g_rtConfig->TicCutoffPercentage );
        FilterByPeakCount( g_rtConfig->MaxPeakCount );

		if( peakPreData.empty() )
			return;

        BOOST_FOREACH(int charge, possibleChargeStates)
        {
		    PeakPreData::iterator precursorWaterLossItr = peakPreData.findNear( mzOfPrecursor - WATER_MONO/charge, g_rtConfig->FragmentMzTolerance, true );
		    if( precursorWaterLossItr != peakPreData.end() )
			    peakPreData.erase( precursorWaterLossItr );

		    PeakPreData::iterator precursorDoubleWaterLossItr = peakPreData.findNear( mzOfPrecursor - 2*WATER_MONO/charge, g_rtConfig->FragmentMzTolerance, true );
		    if( precursorDoubleWaterLossItr != peakPreData.end() )
                peakPreData.erase( precursorDoubleWaterLossItr );
        }

		if( peakPreData.empty() )
			return;

        // results for each possible charge state are stored separately
        resultsByCharge.resize(possibleChargeStates.back());
        BOOST_FOREACH(SearchResultSetType& resultSet, resultsByCharge)
            resultSet.max_ranks( g_rtConfig->MaxResultRank );

		ClassifyPeakIntensities(); // for mvh

        //swap(peakPreData, unfilteredPeakPreData);
        NormalizePeakIntensities(); // for xcorr
        //swap(peakPreData, unfilteredPeakPreData);

		peakCount = (int) peakData.size();

		// Divide the spectrum peak space into equal m/z bins
		//cout << mzUpperBound << "," << mzLowerBound << endl;
		double spectrumMedianMass = totalPeakSpace/2.0;
        double fragMassError = g_rtConfig->FragmentMzTolerance.units == MZTolerance::PPM ? (spectrumMedianMass*g_rtConfig->FragmentMzTolerance.value*1e-6):g_rtConfig->FragmentMzTolerance.value;
		//cout << fragMassError << "," << mOfPrecursor << endl;
		int totalPeakBins = (int) round( totalPeakSpace / ( fragMassError * 2.0f ), 0 );
		initialize( g_rtConfig->NumIntensityClasses+1, g_rtConfig->NumMzFidelityClasses );
		for( PeakData::iterator itr = peakData.begin(); itr != peakData.end(); ++itr )
		{
            if (itr->second.intenClass > 0)
			    ++ intenClassCounts[ itr->second.intenClass-1 ];
		}
		intenClassCounts[ g_rtConfig->NumIntensityClasses ] = totalPeakBins - peakCount;
		//cout << id.nativeID << ": " << intenClassCounts << endl;

        int divider = 0;
	    for( int i=0; i < g_rtConfig->NumMzFidelityClasses-1; ++i )
	    {
		    divider += 1 << i;
		    mzFidelityThresholds[i] = g_rtConfig->FragmentMzTolerance.value * (double)divider / (double)g_rtConfig->minMzFidelityClassCount;
	    }
		mzFidelityThresholds.back() = g_rtConfig->FragmentMzTolerance.value;
        //cout << id.nativeID << ": " << mzFidelityThresholds << endl;

		//totalPeakSpace = peakPreData.rbegin()->first - peakPreData.begin()->first;
		//if( id.nativeID == 1723 )
		//	cout << totalPeakSpace << " " << mzUpperBound << " " << mzLowerBound << endl;

        // we no longer need the raw intensities
		peakPreData.clear();

        // set fragment types
        fragmentTypes.reset();
        if( g_rtConfig->FragmentationAutoRule )
        {
            if( dissociationTypes.count(pwiz::cv::MS_CID) > 0 )
            {
                fragmentTypes[FragmentType_B] = true;
                fragmentTypes[FragmentType_Y] = true;
            }

            if( dissociationTypes.count(pwiz::cv::MS_ETD) > 0 )
            {
                fragmentTypes[FragmentType_B] = false; // override CID
                fragmentTypes[FragmentType_C] = true;
                fragmentTypes[FragmentType_Z_Radical] = true;
            }
        }

        if( fragmentTypes.none() )
            fragmentTypes = g_rtConfig->defaultFragmentTypes;
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
        //cout << id.nativeID << peakPreData.size() << endl;
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

    // the m/z width for xcorr bins
    const double binWidth = Proton;

    #define IS_VALID_INDEX(index,length) (index >=0 && index < length ? true : false)

    /* This function processes the spectra to compute the fast XCorr implemented in Crux. 
       Ideally, this function has to be called prior to spectrum filtering.             
    */
    void Spectrum::NormalizePeakIntensities()
    {
        // Get the number of bins and bin width for the processed peak array
        double massCutOff = precursorMassHypotheses.back().mass + 50;

        int maxBins;
        if (massCutOff > 512)
            maxBins = (int) ceil(massCutOff / 1024) * 1024;
        else
            maxBins = 512;
        
        // Detemine the max mass of a fragmet peak.
        double maxPeakMass = peakPreData.rbegin()->first;

        // Section the original peak array in 10 regions and find the
        // base peak in each region. Also, square-root the peak intensities
        const int numberOfRegions = 10;

        vector<float> basePeakIntensityByRegion(numberOfRegions, 1);
        int regionSelector = (int) (maxPeakMass / numberOfRegions);
        for(PeakPreData::iterator itr = peakPreData.begin(); itr != peakPreData.end(); ++itr)
        {
            itr->second = sqrt(itr->second);
            int mzBin = round(itr->first / binWidth);
            int normalizationIndex = mzBin / regionSelector;
            if( IS_VALID_INDEX( normalizationIndex,numberOfRegions ) )
                basePeakIntensityByRegion[normalizationIndex] = max(basePeakIntensityByRegion[normalizationIndex],
                                                                    itr->second);
        }

        // Normalize peaks in each region from 0 to 50. 
        // Use base peak in each region for normalization. 
        for(PeakPreData::iterator itr = peakPreData.begin(); itr != peakPreData.end(); ++itr)
        {
            int mzBin = round(itr->first / binWidth);
            int normalizationIndex = mzBin / regionSelector;
            if( IS_VALID_INDEX( normalizationIndex,numberOfRegions ) )
                peakData[itr->first].normalizedIntensity = (itr->second / basePeakIntensityByRegion[normalizationIndex]) * 50;
        }
    }

    // Assign an intensity of 50 to fragment ions. 
    // Assign an intensity of 25 to bins neighboring the fragment ions.
    // Assign an intensity of 10 to neutral losses.
    void addXCorrFragmentIon(vector<float>& theoreticalSpectrum, double fragmentMass, int fragmentCharge, FragmentTypes fragmentType)
    {
        int peakDataLength = theoreticalSpectrum.size();
        int mzBin = round(fragmentMass / binWidth);
        if( IS_VALID_INDEX( mzBin, peakDataLength ) )
        {
            theoreticalSpectrum[mzBin] = 50;

            // Fill the neighbouring bins
            if( IS_VALID_INDEX( (mzBin-1), peakDataLength ) )
                theoreticalSpectrum[mzBin-1] = 25;
            if( IS_VALID_INDEX( (mzBin+1), peakDataLength ) )
                theoreticalSpectrum[mzBin+1] = 25;
            
            // Neutral loss peaks
            if(fragmentType == FragmentType_B || fragmentType == FragmentType_Y)
            {
                int NH3LossIndex = round( (fragmentMass - (AMMONIA_MONO / fragmentCharge)) / binWidth );
                if( IS_VALID_INDEX( NH3LossIndex, peakDataLength ) )
                    theoreticalSpectrum[NH3LossIndex] = 10;
            }

            if(fragmentType == FragmentType_B)
            {
                int H20LossIndex = round( (fragmentMass - (WATER_MONO / fragmentCharge)) / binWidth );
                if ( IS_VALID_INDEX( H20LossIndex, peakDataLength ) )
                    theoreticalSpectrum[H20LossIndex] = 10;
            }
        }
    }

    void Spectrum::ComputeXCorrs()
    {
        BOOST_FOREACH(int chargeHypothesis, possibleChargeStates)
        {
            // Get the number of bins and bin width for the processed peak array
            double massCutOff;
            BOOST_REVERSE_FOREACH(PrecursorMassHypothesis& hypothesis, precursorMassHypotheses)
                if (hypothesis.charge == chargeHypothesis)
                {
                    massCutOff = hypothesis.mass + 50;
                    break;
                }

            int maxBins;
            if (massCutOff > 512)
                maxBins = (int) ceil(massCutOff / 1024) * 1024;
            else
                maxBins = 512;

            // populate a vector representation of the peak data
            vector<float> peakDataForXCorr(maxBins, 0);
            int peakDataLength = peakDataForXCorr.size();
            
            for (PeakData::iterator itr = peakData.begin(); itr != peakData.end(); ++itr)
            {
                int mzBin = round(itr->first / binWidth);
                if ( IS_VALID_INDEX(mzBin,maxBins) )
                    peakDataForXCorr[mzBin] = itr->second.normalizedIntensity;
            }

            // Compute the cumulative spectrum
            for (int i = 0; i < peakDataLength; ++i)
                for (int j = i - 75; j <= i + 75; ++j)
                    if ( IS_VALID_INDEX(j,maxBins) )
                        peakDataForXCorr[i] -= (peakDataForXCorr[j] / 151);

            int z = chargeHypothesis-1;
            int maxIonCharge = max(1, z);

            SearchResultSetType& resultSet = resultsByCharge[z];
            typedef SearchResultSetType::RankMap RankMap;

            if (resultSet.empty())
                continue;

            RankMap resultsByRank = resultSet.byRankAndCategory();

            // first=rank, second=vector of tied results
            BOOST_FOREACH(RankMap::value_type& rank, resultsByRank)
            BOOST_FOREACH(const SearchResultSetType::SearchResultPtr& resultPtr, rank.second)
            {
                const SearchResult& result = *resultPtr;

                // Get the expected width of the array
                vector<float> theoreticalSpectrum(peakDataLength, 0);

                size_t seqLength = result.sequence().length();

                // For each peptide bond and charge state
                Fragmentation fragmentation = result.fragmentation(true, true);
                for(int charge = 1; charge <= maxIonCharge; ++charge)
                {
                    for(size_t fragIndex = 0; fragIndex < seqLength; ++fragIndex)
                    {
                        size_t nLength = fragIndex;
                        size_t cLength = seqLength - fragIndex;

                        if(nLength > 0)
                        {
                            if ( fragmentTypes[FragmentType_A] )
                                addXCorrFragmentIon(theoreticalSpectrum, fragmentation.a(nLength, charge), charge, FragmentType_A);
                            if ( fragmentTypes[FragmentType_B] )
                                addXCorrFragmentIon(theoreticalSpectrum, fragmentation.b(nLength, charge), charge, FragmentType_B);
                            if ( fragmentTypes[FragmentType_C] && nLength < seqLength )
                                addXCorrFragmentIon(theoreticalSpectrum, fragmentation.c(nLength, charge), charge, FragmentType_C);
                        }

                        if(cLength > 0)
                        {
                            if ( fragmentTypes[FragmentType_X] && cLength < seqLength )
                                addXCorrFragmentIon(theoreticalSpectrum, fragmentation.x(cLength, charge), charge, FragmentType_X);
                            if ( fragmentTypes[FragmentType_Y] )
                                addXCorrFragmentIon(theoreticalSpectrum, fragmentation.y(cLength, charge), charge, FragmentType_Y);
                            if ( fragmentTypes[FragmentType_Z] )
                                addXCorrFragmentIon(theoreticalSpectrum, fragmentation.z(cLength, charge), charge, FragmentType_Z);
                            if ( fragmentTypes[FragmentType_Z_Radical] )
                                addXCorrFragmentIon(theoreticalSpectrum, fragmentation.zRadical(cLength, charge), charge, FragmentType_Z_Radical);
                        }
                    }
                }
                
                double rawXCorr = 0.0;
                for(int index = 0; index < peakDataLength; ++index)
                    rawXCorr += peakDataForXCorr[index] * theoreticalSpectrum[index];
                (const_cast<Spectrum::SearchResultType&>(result)).XCorr = (rawXCorr / 1e4);
            }
        }
    }

    void Spectrum::computeSecondaryScores()
    {
		//Compute the average and the mode of the MVH and mzFidelity distrubutions
		double averageMVHValue = 0.0;
		double totalComps = 0.0;
		int maxValue = INT_MIN;
		for(flat_map<int,int>::iterator itr = mvhScoreDistribution.begin(); itr!= mvhScoreDistribution.end(); ++itr) {
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
		for(flat_map<int,int>::iterator itr = mvhScoreDistribution.begin(); itr!= mvhScoreDistribution.end(); ++itr) {
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
		for(flat_map<int,int>::iterator itr = mzFidelityDistribution.begin(); itr!= mzFidelityDistribution.end(); ++itr) {
			if((*itr).first==0) {
				continue;
			}
			averageMZFidelity += ((*itr).second * (*itr).first);
			totalComps += (*itr).second;
			maxValue = maxValue < (*itr).second ? (*itr).second : maxValue;
		}
		averageMZFidelity /= totalComps;

		double mzFidelityMode;
		for(flat_map<int,int>::iterator itr = mzFidelityDistribution.begin(); itr!= mzFidelityDistribution.end(); ++itr) {
			if((*itr).second==maxValue) {
				mzFidelityMode = (double) (*itr).first;
				break;
			}
		}
		double massError = 2.5;
		// For each search result
		/*for( SearchResultSetType::const_reverse_iterator rItr = resultSet.rbegin(); rItr != resultSet.rend(); ++rItr )
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
		}*/
	}

    void Spectrum::ScoreSequenceVsSpectrum( SearchResult& result, const string& seq, const vector< double >& seqIons )
    {
        PeakData::iterator peakItr;
		MvIntKey mzFidelityKey;
        //MvIntKey& mvhKey = result.key;
        MvIntKey mvhKey;

		mvhKey.clear();
		mvhKey.resize( g_rtConfig->NumIntensityClasses+1, 0 );
		mzFidelityKey.resize( g_rtConfig->NumMzFidelityClasses+1, 0 );
		result.mvh = 0.0;
		result.mzFidelity = 0.0;
		//result.mzSSE = 0.0;
		//result.newMZFidelity = 0.0;
		//result.mzMAE = 0.0;
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
		    peakItr = peakData.findNear( seqIons[j], g_rtConfig->FragmentMzTolerance );
            
		    STOP_PROFILER(7);

		    // If a peak was found, increment the sequenceInstance's ion correlation triplet
		    if( peakItr != peakData.end() && peakItr->second.intenClass > 0 )
		    {
			    double mzError = fabs( peakItr->first - seqIons[j] );
			    ++mvhKey[ peakItr->second.intenClass-1 ];
			    //result.mzSSE += pow( mzError, 2.0 );
			    //result.mzMAE += mzError;
			    ++mzFidelityKey[ ClassifyError( mzError, mzFidelityThresholds ) ];
			    //int mzFidelityClass = ClassifyError( mzError, g_rtConfig->massErrors );
			    //result.newMZFidelity += g_rtConfig->mzFidelityLods[mzFidelityClass];
			    //result.matchedIons.push_back(peakItr->first);
		    } else
		    {
			    ++mvhKey[ g_rtConfig->NumIntensityClasses ];
			    //result.mzSSE += pow( 2.0 * g_rtConfig->FragmentMzTolerance, 2.0 );
			    //result.mzMAE += 2.0 * g_rtConfig->FragmentMzTolerance;
			    ++mzFidelityKey[ g_rtConfig->NumMzFidelityClasses ];
		    }

	    }
		STOP_PROFILER(6);

		//result.mzSSE /= totalPeaks;
		//result.mzMAE /= totalPeaks;

		// Convert the new mzFidelity score into normal domain.
		//result.newMZFidelity = exp(result.newMZFidelity);

		double mvh = 0.0;

        result.fragmentsUnmatched = mvhKey.back();

		START_PROFILER(8);
		if( result.fragmentsUnmatched != totalPeaks )
		{
            int fragmentsPredicted = accumulate( mvhKey.begin(), mvhKey.end(), 0 );
			result.fragmentsMatched = fragmentsPredicted - result.fragmentsUnmatched;

			//int numHits = accumulate( intenClassCounts.begin(), intenClassCounts.end()-1, 0 );
			int numVoids = intenClassCounts.back();
			int totalPeakBins = numVoids + peakCount;

			for( size_t i=0; i < intenClassCounts.size(); ++i ) {
				mvh += lnCombin( intenClassCounts[i], mvhKey[i] );
			}
			mvh -= lnCombin( totalPeakBins, fragmentsPredicted );

			result.mvh = -mvh;


			int N;
			double sum1 = 0, sum2 = 0;
			int totalPeakSpace = numVoids + fragmentsPredicted;
			double pHits = (double) fragmentsPredicted / (double) totalPeakSpace;
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
}
}
