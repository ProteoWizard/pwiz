#include "stdafx.h"
#include "myrimatchSpectrum.h"

using namespace freicore;

namespace std
{
	ostream& operator<< ( ostream& o, const freicore::myrimatch::PeakInfo& rhs )
	{
		return o << rhs.intenClass;
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

		// Secondly, determine the neutral mass of the precursor (m/z * z - z)
		mOfPrecursor = mzOfPrecursor * id.charge - ( id.charge * PROTON );
		mOfUnadjustedPrecursor = mOfPrecursor;
		//if( id.index == 6234 ) cout << mOfPrecursor << endl;
		//cout << g_residueMap->GetMassOfResidues( "AGLLGLLEEMR", false ) << endl;

		// Eliminate peaks above the precursor's mass with a given tolerance
		double maxPeakMass = mOfPrecursor + PROTON + g_rtConfig->PrecursorMassTolerance.back();
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

			Deisotope( g_rtConfig->IsotopeMzTolerance );

			if( g_rtConfig->MakeSpectrumGraphs )
				writeToSvgFile( "-deisotoped" + g_rtConfig->OutputSuffix );
		}

		if( g_rtConfig->AdjustPrecursorMass )
		{
			double originalPrecursorMass = mOfPrecursor;
			double originalPrecursorMz = mzOfPrecursor;
			double bestPrecursorAdjustment = 0.0f;
			double maxSumOfProducts = 0.0f;
			map< double, double > AdjustmentResults;

			for( mOfPrecursor += g_rtConfig->MinPrecursorAdjustment;
				 mOfPrecursor <= originalPrecursorMass + g_rtConfig->MaxPrecursorAdjustment;
				 mOfPrecursor += g_rtConfig->PrecursorAdjustmentStep )
			{
				mzOfPrecursor = ( mOfPrecursor + ( id.charge * PROTON ) ) / id.charge;

				double sumOfProducts = FindComplements( g_rtConfig->ComplementMzTolerance, g_rtConfig->PreferIntenseComplements );

				if( sumOfProducts > maxSumOfProducts )
				{
					maxSumOfProducts = sumOfProducts;
					bestPrecursorAdjustment = mOfPrecursor - originalPrecursorMass;
				}

				AdjustmentResults[ mOfPrecursor ] = sumOfProducts;
			}

			map< double, vector< double > > adjustmentToMzMap;
			for( map< double, double >::iterator itr = AdjustmentResults.begin(); itr != AdjustmentResults.end(); ++itr )
				adjustmentToMzMap[ itr->second ].push_back( itr->first );
			int n=0;
			for( map< double, vector< double > >::reverse_iterator itr = adjustmentToMzMap.rbegin();
				 itr != adjustmentToMzMap.rend() && n < g_rtConfig->NumSearchBestAdjustments;
				 ++itr, ++n )
			{
				 for( size_t i=0; i < itr->second.size(); ++i )
					mOfPrecursorList.push_back( itr->second[i] );
			}

			if( maxSumOfProducts > 0 )
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
		} else
			mOfPrecursorList.push_back( mOfPrecursor );

		ClassifyPeakIntensities();
		FindComplements( g_rtConfig->ComplementMzTolerance );

		peakCount = (int) peakData.size();

		int totalPeakBins = (int) round( totalPeakSpace / ( g_rtConfig->FragmentMzTolerance * 2.0f ), 0 );
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
		    mzFidelityThresholds[i] = g_rtConfig->FragmentMzTolerance * (double)divider / (double)g_rtConfig->minMzFidelityClassCount;
	    }
	    mzFidelityThresholds.back() = g_rtConfig->FragmentMzTolerance;
        //cout << id.index << ": " << mzFidelityThresholds << endl;

		//totalPeakSpace = peakPreData.rbegin()->first - peakPreData.begin()->first;
		//if( id.index == 1723 )
		//	cout << totalPeakSpace << " " << mzUpperBound << " " << mzLowerBound << endl;

		// If graphs will be drawn, the predata must be retained, otherwise it's unnecessary
		if( !g_rtConfig->MakeSpectrumGraphs /*&& g_rtConfig->DeisotopingTestMode == 0*/ )
			peakPreData.clear();
        // set fragment types
        fragmentTypes.reset();
        if( g_rtConfig->FragmentationAutoRule )
        {
            switch( dissociationType )
            {
                case pwiz::MS_CID:
                    fragmentTypes[FragmentType_B] = true;
                    fragmentTypes[FragmentType_Y] = true;
                    break;
                case pwiz::MS_ETD:
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
