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

#include "stdafx.h"
#include "pepitomeSpectrum.h"

using namespace freicore;

namespace std
{
    ostream& operator<< ( ostream& o, const freicore::pepitome::PeakInfo& rhs )
    {
        return o << rhs.intenClass << "," << rhs.intensityRank << "," << rhs.normIntensity << "," << rhs.rawIntensity;
    }
}

namespace freicore
{
    namespace pepitome
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

            if(peakPreData.empty())
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
                    p.massType = precursorMzType;
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
                    p.massType = precursorMzType;
                    p.charge = charge;
                    precursorMassHypotheses.push_back(p);
                }
            }

            // sort hypotheses ascending by mass
            sort(precursorMassHypotheses.begin(), precursorMassHypotheses.end());

            // filter out peaks above the largest precursor hypthesis' mass
            peakPreData.erase( peakPreData.upper_bound( precursorMassHypotheses.back().mass + g_rtConfig->AvgPrecursorMzTolerance ), peakPreData.end() );

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

            FilterByTIC( g_rtConfig->TicCutoffPercentage );
            FilterByPeakCount( g_rtConfig->MaxPeakCount );

            // results for each possible charge state are stored separately
            resultsByCharge.resize(possibleChargeStates.back());
            BOOST_FOREACH(SearchResultSetType& resultSet, resultsByCharge)
                resultSet.max_ranks( g_rtConfig->MaxResultRank );

            ClassifyPeakIntensities(true, true);

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
                ++ intenClassCounts[ itr->second.intenClass-1 ];
            }
            intenClassCounts[ g_rtConfig->NumIntensityClasses ] = totalPeakBins - peakCount;
            //cout << id.index << ": " << intenClassCounts << endl;

            int divider = 0;
	        for( int i=0; i < g_rtConfig->NumMzFidelityClasses-1; ++i )
	        {
		        divider += 1 << i;
		        mzFidelityThresholds[i] = g_rtConfig->FragmentMzTolerance.value * (double)divider / (double)g_rtConfig->minMzFidelityClassCount;
	        }
		    mzFidelityThresholds.back() = g_rtConfig->FragmentMzTolerance.value;

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

        void Spectrum::ClassifyPeakIntensities(bool rankPeaks, bool DotProduct)
        {
            double TIC = 0.0;
            // Sort peaks by intensity.
            // Use multimap because multiple peaks can have the same intensity.
            typedef multimap< double, double > IntenSortedPeakPreData;
            IntenSortedPeakPreData intenSortedPeakPreData;
            for( PeakPreData::iterator itr = peakPreData.begin(); itr != peakPreData.end(); ++itr )
            {
                IntenSortedPeakPreData::iterator iItr = intenSortedPeakPreData.insert( make_pair( itr->second, itr->second ) );
                iItr->second = itr->first;
                TIC += sqrt(iItr->first);
            }

            //cout << id.index << peakPreData.size() << endl;
            peakPreData.clear();
            peakData.clear();

            if(rankPeaks)
            {
                IntenSortedPeakPreData::reverse_iterator iItr = intenSortedPeakPreData.rbegin();
                double prevPeakInten = iItr->first;
                int prevPeakRank = 1;
                for( ; iItr != intenSortedPeakPreData.rend(); ++iItr )
                {
                    double mz = iItr->second;
                    double inten = iItr->first;
                    peakPreData[ mz ] = inten;
                    peakData[ mz ].rawIntensity = inten;
                    peakData[ mz ].normIntensity = inten/TIC;

                    if(inten != prevPeakInten)
                    {
                        ++prevPeakRank; 
                        prevPeakInten = inten;
                    }
                    peakData[ mz ].intensityRank = prevPeakRank;
                }
            }
            // Restore the sorting order to be based on MZ
            IntenSortedPeakPreData::reverse_iterator r_iItr = intenSortedPeakPreData.rbegin();

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

        inline int sign(double x)
        {
            if(x>0)
                return 1;
            else if(x<0)
                return -1;
            else 
                return 0;
        }

        void Spectrum::ScoreSpectrumVsSpectrum( SearchResult& result, const PeakData& libPeaks )
        {
            PeakData::iterator peakItr;
            
            MvIntKey mzFidelityKey;
            MvIntKey mvhKey;
            mvhKey.clear();
		    mvhKey.resize( g_rtConfig->NumIntensityClasses+1, 0 );
		    mzFidelityKey.resize( g_rtConfig->NumMzFidelityClasses+1, 0 );

            result.mvh = 0.0;
            result.mzSSE = 0.0;
            result.mzFidelity = 0.0;
            result.newMZFidelity = 0.0;
            result.mzMAE = 0.0;
            result.matchedIons.clear();

            result.hgt = 0.0;
            result.kendallTau = -1.0;
            result.kendallPVal = 0.0;
            result.rankingScore = 0.0;

            START_PROFILER(6);
            int totalPeaks = (int) libPeaks.size();
            map<double,double> matchedIntensityPairs;

            for( PeakData::const_iterator iter = libPeaks.begin(); iter != libPeaks.end(); ++iter )
            {
                double fragIonMass = (*iter).first;
                PeakInfo peakInfo = (*iter).second;
                // skip theoretical ions outside the scan range of the spectrum
                if( fragIonMass < mzLowerBound || fragIonMass > mzUpperBound )
                {
                    --totalPeaks; // one less ion to consider because it's out of the scan range
                    continue;
                }

                START_PROFILER(7);
                // Find the fragment ion peak. Consider the fragment ion charge state while setting the
                // mass window for the fragment ion lookup.
                peakItr = peakData.findNear( fragIonMass, g_rtConfig->FragmentMzTolerance );
                STOP_PROFILER(7);

                // If a peak was found, increment the sequenceInstance's ion correlation triplet
                if( peakItr != peakData.end() && peakItr->second.intenClass > 0 )
                {
                    double mzError = peakItr->first - fragIonMass;
                    if(g_rtConfig->FragmentMzTolerance.units == MZTolerance::PPM)
                        mzError = (mzError/fragIonMass)*1.0e6;
                    ++mvhKey[ peakItr->second.intenClass-1 ];
                    ++mzFidelityKey[ ClassifyError( mzError, mzFidelityThresholds ) ];
                    result.mzSSE += pow( mzError, 2.0 );
                    result.mzMAE += fabs(mzError);
                    int mzFidelityClass = ClassifyError( fabs( mzError ), g_rtConfig->massErrors );
                    result.newMZFidelity += g_rtConfig->mzFidelityLods[mzFidelityClass];
                    matchedIntensityPairs.insert(make_pair(peakItr->second.intensityRank , peakInfo.intensityRank));
                } else
                {
                    ++mvhKey[ g_rtConfig->NumIntensityClasses ];
                    ++mzFidelityKey[ g_rtConfig->NumMzFidelityClasses ];
                    result.mzSSE += pow( 2.0 * g_rtConfig->FragmentMzTolerance.value, 2.0 );
                    result.mzMAE += 2.0 * g_rtConfig->FragmentMzTolerance.value;
                }
            }
            STOP_PROFILER(6);
            
            // Do not bother scoring if more than 20% of peaks fell outside the spectrum bounds
            double percentPeaksOutOfBounds = (double) (libPeaks.size()-totalPeaks)/((double)libPeaks.size());
            if(percentPeaksOutOfBounds > 0.2)
                return;

            double n = (double) matchedIntensityPairs.size();
            if(n >=2 )
            {
                // Compute the kendall rank correlation coeffcient between matched peak pairs
                for(map<double,double>::iterator iIter = matchedIntensityPairs.begin(); iIter != matchedIntensityPairs.end(); ++iIter)
                    for(map<double,double>::iterator jIter = matchedIntensityPairs.begin(); jIter != iIter; ++jIter)
                        result.kendallTau += sign(iIter->first-jIter->first)*sign(iIter->second-jIter->second);
                result.kendallTau /= (0.5*n*(n-1));
                double tauVar = (2.0*(2.0*n+5.0))/((9.0*n*(n-1.0)));
                normal tauDist(0.0,sqrt(tauVar));
                result.kendallPVal = -1*log(cdf(complement(tauDist, result.kendallTau)));
            }

            result.mzSSE /= totalPeaks;
            result.mzMAE /= totalPeaks;

            // Convert the new mzFidelity score into normal domain.
            result.newMZFidelity = exp(result.newMZFidelity);

            double mvh = 0.0;

            result.fragmentsUnmatched = mvhKey.back();

            START_PROFILER(8);
            if( result.fragmentsUnmatched != totalPeaks )
            {
                int fragmentsPredicted = accumulate( mvhKey.begin(), mvhKey.end(), 0 );
                result.fragmentsMatched = fragmentsPredicted - result.fragmentsUnmatched;

                int numVoids = intenClassCounts.back();
                int totalPeakBins = numVoids + peakCount;

                for( size_t i=0; i < intenClassCounts.size(); ++i ) {
                    mvh += lnCombin( intenClassCounts[i], mvhKey[i] );
                }
                mvh -= lnCombin( totalPeakBins, fragmentsPredicted );
                result.mvh = -mvh;

                // Compute the p-value of matching more peaks by random chance. This test performs
                // a hyper-geometric test with number of matched peaks and number of available peak bins.
                int peakMatches = result.fragmentsMatched;
                //cout << libPeaks.size() << "," << peakCount << "," << totalPeaks << "," << totalPeakBins << "," << peakMatches << flush << endl;
                hypergeometric_distribution<double> hgd(peakCount, totalPeaks, totalPeakBins);
                result.hgt = -1.0 * log(cdf(complement(hgd,min(peakMatches,peakCount))) + pdf(hgd,min(peakMatches,peakCount)));
                // Fuse the kendall's tau test with hyper-geometric test using Fisher's method
                result.rankingScore = -2.0 * (-1.0*result.hgt + -1.0*result.kendallPVal);
                //cout << result.hgt << "," << result.kendallPVal << "," << result.rankingScore << endl;
                result.rankingScore = -1.0 * log(cdf(complement(*(g_rtConfig->chiDist), result.rankingScore)));

                int N;
                double sum1 = 0, sum2 = 0;
                int totalPeakSpace = numVoids + fragmentsPredicted;
                double pHits = (double) fragmentsPredicted / (double) totalPeakSpace;
                double pMisses = 1.0 - pHits;

                N = accumulate( mzFidelityKey.begin(), mzFidelityKey.end(), 0 );
                int p = 0;
                for( int i=0; i < g_rtConfig->NumMzFidelityClasses; ++i )
                {
                    p = 1 << i;
                    double pKey = pHits * ( (double) p / (double) g_rtConfig->minMzFidelityClassCount );
                    sum1 += log( pow( pKey, mzFidelityKey[i] ) );
                    sum2 += g_lnFactorialTable[ mzFidelityKey[i] ];
                }
                sum1 += log( pow( pMisses, mzFidelityKey.back() ) );
                sum2 += g_lnFactorialTable[ mzFidelityKey.back() ];
                result.mzFidelity = -1.0 * double( ( g_lnFactorialTable[ N ] - sum2 ) + sum1 );
            }

            STOP_PROFILER(8);
        }

        void Spectrum::computeSecondaryScores() 
        {
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
        }

    }
}
