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

#include "stdafx.h"
#include "tagreconSpectrum.h"

using namespace freicore;

namespace std
{
	ostream& operator<< ( ostream& o, const freicore::tagrecon::PeakInfo& rhs )
	{
		return o << rhs.intenClass;
	}
}

namespace freicore
{
namespace tagrecon
{
	/**!
		Preprocess() function take an experimental spectrum and processes it to 
		locate neutral and water losses from the precursor ion, performs deisotoping
		(user choice), corrects for precursor mass (user choice), classifies peak
		intesities into 3 or more classes (user configurable), and finds all complementary
		ion pairs in the spectrum.
	*/
	void Spectrum::Preprocess()
	{
		PeakPreData::iterator itr;
		PeakPreData::reverse_iterator r_itr;

		if( mzOfPrecursor < 1 )
		{
			peakPreData.clear();
			return;
		}

		// Secondly, determine the neutral mass of the precursor (m/z * z - z)
		mOfPrecursor = mzOfPrecursor * id.charge - ( id.charge * PROTON );
		mOfUnadjustedPrecursor = mOfPrecursor;
		//if( id.index == 6234 ) cout << mOfPrecursor << endl;
		//cout << g_residueMap->GetMassOfResidues( "AGLLGLLEEMR", false ) << endl;

		// Eliminate peaks above the precursor's mass with a given tolerance
		float maxPeakMass = mOfPrecursor + PROTON + g_rtConfig->PrecursorMassTolerance.back();
		itr = peakPreData.upper_bound( maxPeakMass );
		peakPreData.erase( itr, peakPreData.end() );

		if( peakPreData.empty() )
			return;

		// Thirdly, store the bounds of the spectrum before eliminating any peaks
		mzLowerBound = peakPreData.begin()->first;
		mzUpperBound = peakPreData.rbegin()->first;
		totalPeakSpace = mzUpperBound - mzLowerBound;

		if( g_rtConfig->MakeSpectrumGraphs )
				writeToSvgFile( "-unprocessed" + g_rtConfig->OutputSuffix );

		// Cut off the peaks based on % TIC (User configurable)
		FilterByTIC( g_rtConfig->TicCutoffPercentage );

		// Locate water loss ions of the precursor ion
		PeakPreData::iterator precursorWaterLossItr = peakPreData.findNear( mzOfPrecursor - WATER_MONO/id.charge, g_rtConfig->FragmentMzTolerance, true );
		PeakPreData::iterator precursorDoubleWaterLossItr = peakPreData.findNear( mzOfPrecursor - 2*WATER_MONO/id.charge, g_rtConfig->FragmentMzTolerance, true );
        bool eraseWaterLoss = precursorWaterLossItr != peakPreData.end();
        bool eraseDoubleWaterLoss = precursorDoubleWaterLossItr != peakPreData.end() && precursorWaterLossItr != precursorDoubleWaterLossItr;
		if( eraseWaterLoss ) peakPreData.erase( precursorWaterLossItr );
		if( eraseDoubleWaterLoss ) peakPreData.erase( precursorDoubleWaterLossItr );

		if( g_rtConfig->MakeSpectrumGraphs )
			writeToSvgFile( "-filtered" + g_rtConfig->OutputSuffix );

		// Create a deisotoped version of the spectrum for finding complements
		PeakPreData originalPeakPreData;
		//PeakData originalPeakData;

		if( g_rtConfig->DeisotopingMode > 0 || g_rtConfig->AdjustPrecursorMass )
		{
			if( g_rtConfig->DeisotopingMode == 0 )
			{
				originalPeakPreData = peakPreData;
				//originalPeakData = peakData;
			}

			// Deisotope the spectrum
			Deisotope( g_rtConfig->IsotopeMzTolerance );

			if( g_rtConfig->MakeSpectrumGraphs )
				writeToSvgFile( "-deisotoped" + g_rtConfig->OutputSuffix );
		}

		// Adjust the precursor mass if user desired to do so.
		// Change the precursor mass from Minimum to Maximum 
		// adjustment in a small mass steps. Find the number of
		// complements at each mass step and calculate their total
		// intensity. Use the mass of the precursor that gave the 
		// maximum total intensity to correct the experimental precursor mass.
		if( g_rtConfig->AdjustPrecursorMass )
		{
			float originalPrecursorMass = mOfPrecursor;
			float originalPrecursorMz = mzOfPrecursor;
			float bestPrecursorAdjustment = 0.0f;
			float maxSumOfProducts = 0.0f;
			map< float, float > AdjustmentResults;

			// Step between minimumPrecursorAdjustment to MaxPrecursorAdjustment in small
			// mass steps of PrecursorAdjustmentStep mangnitude
			for( mOfPrecursor += g_rtConfig->MinPrecursorAdjustment;
				 mOfPrecursor <= originalPrecursorMass + g_rtConfig->MaxPrecursorAdjustment;
				 mOfPrecursor += g_rtConfig->PrecursorAdjustmentStep )
			{
				// Get the new precursor m/z
				mzOfPrecursor = ( mOfPrecursor + ( id.charge * PROTON ) ) / id.charge;

				// Find out the total intensity of number of complementary pairs
				float sumOfProducts = FindComplements( g_rtConfig->ComplementMzTolerance, g_rtConfig->PreferIntenseComplements );

				// Remember the PrecursorAdjustmentStep that gave the maximum
				// complementary intensity
				if( sumOfProducts > maxSumOfProducts )
				{
					maxSumOfProducts = sumOfProducts;
					bestPrecursorAdjustment = mOfPrecursor - originalPrecursorMass;
				}

				// Store the number of complementary pairs found at each adjustment step.
				AdjustmentResults[ mOfPrecursor ] = sumOfProducts;
			}

			// Remember the precursor masses for the few best precursor adjustments
			map< float, vector< float > > adjustmentToMzMap;
			for( map< float, float >::iterator itr = AdjustmentResults.begin(); itr != AdjustmentResults.end(); ++itr )
				adjustmentToMzMap[ itr->second ].push_back( itr->first );
			int n=0;
			for( map< float, vector< float > >::reverse_iterator itr = adjustmentToMzMap.rbegin();
				 itr != adjustmentToMzMap.rend() && n < g_rtConfig->NumSearchBestAdjustments;
				 ++itr, ++n )
			{
				 for( size_t i=0; i < itr->second.size(); ++i )
					mOfPrecursorList.push_back( itr->second[i] );
			}

			// Update the precursor mass with bestPrecursorAdjustment that gave
			// maximum total complementary intensity.
			if( maxSumOfProducts > 0.0f )
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
				cout << oldPrecursor << " (" << mOfPrecursorFixed << ") corrected by " << mzOfPrecursor - oldPrecursor <<
						" to " << mzOfPrecursor << " (" << mOfPrecursor << ") " << endl;*/

				cout << AdjustmentResults << endl;
			}

			if( g_rtConfig->DeisotopingMode == 0 )
			{
				peakPreData = originalPeakPreData;
				//peakData = originalPeakData;
			}
		} else {
			// If user doesn't want any precursor adjustment then so
			// be it.
			mOfPrecursorList.push_back( mOfPrecursor );
		}

		// Classify peak intensities
		ClassifyPeakIntensities();
		// Find complementary fragment ions
		FindComplements( g_rtConfig->ComplementMzTolerance );

		peakCount = (int) peakData.size();
        //cout << id.id << ":" << peakCount << endl;

		// Compute the total number of peak bins
		int totalPeakBins = (int) round( totalPeakSpace / ( g_rtConfig->FragmentMzTolerance * 2.0f ), 0 );
		// Compute the number of peaks in each intensity and m/z fidelity classes
		initialize( g_rtConfig->NumIntensityClasses+1, g_rtConfig->NumMzFidelityClasses );
		for( PeakData::iterator itr = peakData.begin(); itr != peakData.end(); ++itr )
		{
			++ intenClassCounts[ itr->second.intenClass-1 ];
		}
		intenClassCounts[ g_rtConfig->NumIntensityClasses ] = totalPeakBins - peakCount;
		//cout << id.index << ": " << intenClassCounts << endl;

		// Compute the m/z fidelity score. First divide the fragment m/z tolerance (fmt) around an 
		// expected m/z into 3 classes (user configurable) with each successive class twice as big 
		// the previous class, e.g. 1:2:4 ratio (fmt, fmt*2, fmt*4). Compute the number of fragment
		// ions that were matched between exprimental and theoretical spectra with a mass tolerance
		// of fmt, fmt*2, and fmt*4 etc. Then apply multinomial dist to compute the chance of getting
		// a random match if x number of peaks matched from class 1 (fmt), y number of peaks matched
		// from class 2 (fmt * 2), and z number of peaks matched from class 3 (fmt * 4). This scoring
		// is very similar to intensity scoring in MyriMatch algorithm (Journal of Proteome Reasearch;
		// 2007; 6(2); 654-661)
        int divider = 0;
	    for( int i=0; i < g_rtConfig->NumMzFidelityClasses-1; ++i )
	    {
		    divider += 1 << i;
		    mzFidelityThresholds[i] = g_rtConfig->FragmentMzTolerance * (float)divider / (float)g_rtConfig->minMzFidelityClassCount;
	    }
	    mzFidelityThresholds.back() = g_rtConfig->FragmentMzTolerance;
        //cout << id.index << ": " << mzFidelityThresholds << endl;
 
		//totalPeakSpace = peakPreData.rbegin()->first - peakPreData.begin()->first;
		//if( id.index == 1723 )
		//	cout << totalPeakSpace << " " << mzUpperBound << " " << mzLowerBound << endl;

		// If graphs will be drawn, the predata must be retained, otherwise it's unnecessary
		if( !g_rtConfig->MakeSpectrumGraphs /*&& g_rtConfig->DeisotopingTestMode == 0*/ )
			peakPreData.clear();

        fragmentTypes.reset();
        if( g_rtConfig->FragmentationAutoRule )
        {
            switch( dissociationType )
            {
                case pwiz::cv::MS_CID:
                    fragmentTypes[FragmentType_B] = true;
                    fragmentTypes[FragmentType_Y] = true;
                    break;
                case pwiz::cv::MS_ETD:
                    fragmentTypes[FragmentType_C] = true;
                    fragmentTypes[FragmentType_Z_Radical] = true;
                    break;
                default:
                    break;
            }
        }

        if( fragmentTypes.none() )
            fragmentTypes = g_rtConfig->defaultFragmentTypes;
	}
}
}
