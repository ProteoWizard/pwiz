//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#define PWIZ_DOCTEST_NO_MAIN

#include "pwiz/data/msdata/MSData.hpp"
#include "PrecursorMassFilter.hpp"
#include "ThresholdFilter.hpp"
#include "MS2Deisotoper.hpp"
#include "MS2NoiseFilter.hpp"
#include "SpectrumList_PeakFilter.hpp"
#include "SpectrumList_ZeroSamplesFilter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::analysis;

ostream* os_ = 0;

ostream& operator<< (ostream& os, const vector<double>& v)
{
    os << "(";
    for (size_t i=0; i < v.size(); ++i)
        os << " " << v[i];
    os << " )";
    return os;
}

vector<double> parseDoubleArray(const string& doubleArray)
{
    vector<double> doubleVector;
    vector<string> tokens;
    bal::split(tokens, doubleArray, bal::is_space());
    if (!tokens.empty() && !tokens[0].empty())
        for (size_t i=0; i < tokens.size(); ++i)
            doubleVector.push_back(lexical_cast<double>(tokens[i]));
    return doubleVector;
}


////////////////////////////////////////////////////////////////////////////
//  ETD/ECD Filter test
////////////////////////////////////////////////////////////////////////////
struct TestETDMassFilter
{
    // space-delimited doubles
    const char* inputMZArray;
    const char* inputIntensityArray;
    const char* outputMZArray;
    const char* outputIntensityArray;

    double matchingTolerance;
    bool usePPM;
    bool hasCharge;
    bool removePrecursor;
    bool removeReducedChargePrecursors;
    bool removeNeutralLossPrecursors;
    bool blanketRemovalofNeutralLoss;
};

#define PRECURSOR_CHARGE 3
#define PRECURSOR_MZ 445.34

TestETDMassFilter testETDMassFilterData[] =
{
    {    
        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0", 
        "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 160 170 180 190 200 210 220 230 240 250 260 270 280", 
        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0", 
        "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 160 170 180 190 200 210 220 230 240 250 260 270 280", 
        0.1234, false, false, false, false, false, false
    }, // do nothing

    {    
        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 445.35 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0", 
        "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 160 170 180 190 200 210 220 230 240 250 260 270 280 290", 
        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0", 
        "10 20 30 40 50 60 70 80 90 100 110 120 130 140 170 180 190 200 210 220 230 240 250 260 270 280 290", 
        0.1234, false, true, true, false, false, false
    }, // remove precursor only

    {    
        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 445.35 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0", 
        "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 160 170 180 190 200 210 220 230 240 250 260 270 280 290", 
        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0", 
        "10 20 30 40 50 60 70 80 90 100 110 120 130 140 170 180 190 200 210 220 230 240 250 260 270 280 290", 
        0.1234, false, false, true, false, false, false
    }, // remove precursor without charge state

    {    
        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 668.01 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0", 
        "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 155 160 170 180 190 200 210 220 230 240 250 260 270 280", 
        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0", 
        "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 160 170 180 190 200 210 220 230 240 250 260 270 280", 
        0.1234, false, true, false, true, false, false
    }, // remove charge reduced precursors only

    {    
        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 668.01 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0", 
        "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 155 160 170 180 190 200 210 220 230 240 250 260 270 280", 
        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0", 
        "10 20 30 40 50 60 70 80 90 100 110 120 130 140 160 170 180 190 200 210 220 230 240 250 260 270 280", 
        0.1234, false, true, true, true, false, false
    }, // remove precursor and charge reduced precursors

    {    
        "100 120 445.34 667.51 668.01 1335.02 1336.02 1400.", 
        "10 20 30 40 50 60 70 80", 
        "100 120", 
        "10 20", 
        0.01, false, true, true, true, true, false
    }, // remove precursor charge reduced precursors, and neutral losses

    {    
        "100 120 445.34 667.51 668.01 1335.02 1336.02 1400.", 
        "10 20 30 40 50 60 70 80", 
        "100 120", 
        "10 20", 
        0.01, false, true, true, true, true, true
    }, // remove precursor charge reduced precursors, and neutral losses -- blanket removal of neutral losses (60 Da window)

};

const size_t testETDMassFilterDataSize = sizeof(testETDMassFilterData) / sizeof(TestETDMassFilter);

void testPrecursorMassRemoval()
{
    for (size_t i=0; i < testETDMassFilterDataSize; ++i)
    {
        SpectrumListSimple* sl = new SpectrumListSimple;
        SpectrumListPtr originalList(sl);
        SpectrumPtr s(new Spectrum);
        sl->spectra.push_back(s);

        TestETDMassFilter& t = testETDMassFilterData[i];

        vector<double> inputMZArray = parseDoubleArray(t.inputMZArray);
        vector<double> inputIntensityArray = parseDoubleArray(t.inputIntensityArray);
        s->set(MS_MSn_spectrum);
        s->set(MS_ms_level, 2);

        s->setMZIntensityArrays(inputMZArray, inputIntensityArray, MS_number_of_detector_counts);
        s->precursors.resize(1);
        s->precursors[0].activation.set(MS_electron_transfer_dissociation);
        s->precursors[0].selectedIons.resize(1);
        s->precursors[0].selectedIons[0].set(MS_selected_ion_m_z, PRECURSOR_MZ, MS_m_z);

        if (t.hasCharge)
            s->precursors[0].selectedIons[0].set(MS_charge_state, PRECURSOR_CHARGE);

        MZTolerance tol(t.matchingTolerance, t.usePPM ? MZTolerance::PPM : MZTolerance::MZ);
        SpectrumDataFilterPtr filter;
        if (t.removeNeutralLossPrecursors)
        {
            PrecursorMassFilter::Config params(tol, t.removePrecursor, t.removeReducedChargePrecursors, t.blanketRemovalofNeutralLoss);
            filter.reset(new PrecursorMassFilter(params));
        }
        else
        {
            PrecursorMassFilter::Config params(tol, t.removePrecursor, t.removeReducedChargePrecursors, t.blanketRemovalofNeutralLoss, 0);
            filter.reset(new PrecursorMassFilter(params));
        }
        SpectrumListPtr peakFilter(new SpectrumList_PeakFilter(originalList, filter));

        SpectrumPtr pFiltered = peakFilter->spectrum(0, true);

        vector<double> outputMZArray = parseDoubleArray(t.outputMZArray);
        vector<double> outputIntensityArray = parseDoubleArray(t.outputIntensityArray);

        BinaryData<double>& resultMZArray = pFiltered->getMZArray()->data;
        BinaryData<double>& resultIntensityArray = pFiltered->getIntensityArray()->data;

        unit_assert(resultMZArray.size() == outputMZArray.size());
        unit_assert(resultIntensityArray.size() == outputIntensityArray.size());
        for (size_t ii=0; ii < outputMZArray.size(); ++ii)
        {
            unit_assert_equal(resultMZArray[ii], outputMZArray[ii], 0.001);
            unit_assert_equal(resultIntensityArray[ii], outputIntensityArray[ii], 0.001);
        }
    }
}

////////////////////////////////////////////////////////////////////////////
//  Thresholder test
////////////////////////////////////////////////////////////////////////////

struct TestThresholder
{
    // space-delimited doubles
    const char* inputMZArray;
    const char* inputIntensityArray;
    const char* outputMZArray;
    const char* outputIntensityArray;

    ThresholdFilter::ThresholdingBy_Type byType;
    double threshold;
    ThresholdFilter::ThresholdingOrientation orientation;
};

TestThresholder testThresholders[] =
{
    // test empty spectrum
    { "", "", "", "", ThresholdFilter::ThresholdingBy_AbsoluteIntensity, 0.1, ThresholdFilter::Orientation_MostIntense },
    { "", "", "", "", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 0.1, ThresholdFilter::Orientation_MostIntense },
    { "", "", "", "", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.1, ThresholdFilter::Orientation_MostIntense },
    { "", "", "", "", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.99, ThresholdFilter::Orientation_MostIntense },
    { "", "", "", "", ThresholdFilter::ThresholdingBy_Count, 5, ThresholdFilter::Orientation_MostIntense },

    // test one peak spectrum
    { "1", "10", "1", "10", ThresholdFilter::ThresholdingBy_AbsoluteIntensity, 0.1, ThresholdFilter::Orientation_MostIntense },
    { "1", "10", "1", "10", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 0.1, ThresholdFilter::Orientation_MostIntense },
    { "1", "10", "1", "10", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.1, ThresholdFilter::Orientation_MostIntense },
    { "1", "10", "1", "10", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.99, ThresholdFilter::Orientation_MostIntense },
    { "1", "10", "1", "10", ThresholdFilter::ThresholdingBy_Count, 5, ThresholdFilter::Orientation_MostIntense },

    // test two peak spectrum with a zero data point
    { "1 2", "10 0", "1", "10", ThresholdFilter::ThresholdingBy_AbsoluteIntensity, 0.1, ThresholdFilter::Orientation_MostIntense },
    { "1 2", "10 0", "1", "10", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 0.1, ThresholdFilter::Orientation_MostIntense },
    { "1 2", "10 0", "1", "10", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.1, ThresholdFilter::Orientation_MostIntense },
    { "1 2", "10 0", "1", "10", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.99, ThresholdFilter::Orientation_MostIntense },
    { "1 2", "10 0", "1 2", "10 0", ThresholdFilter::ThresholdingBy_Count, 5, ThresholdFilter::Orientation_MostIntense },

    // absolute thresholding, keeping the most intense points
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 3 4 5", "10 20 30 20 10", ThresholdFilter::ThresholdingBy_AbsoluteIntensity, 5, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "2 3 4", "20 30 20", ThresholdFilter::ThresholdingBy_AbsoluteIntensity, 10, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "2 3 4", "20 30 20", ThresholdFilter::ThresholdingBy_AbsoluteIntensity, 15, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "", "", ThresholdFilter::ThresholdingBy_AbsoluteIntensity, 30, ThresholdFilter::Orientation_MostIntense },

    // absolute thresholding, keeping the least intense points
    { "1 2 3 4 5", "10 20 30 20 10", "", "", ThresholdFilter::ThresholdingBy_AbsoluteIntensity, 5, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "", "", ThresholdFilter::ThresholdingBy_AbsoluteIntensity, 10, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 5", "10 10", ThresholdFilter::ThresholdingBy_AbsoluteIntensity, 15, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 4 5", "10 20 20 10", ThresholdFilter::ThresholdingBy_AbsoluteIntensity, 30, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 3 4 5", "10 20 30 20 10", ThresholdFilter::ThresholdingBy_AbsoluteIntensity, 50, ThresholdFilter::Orientation_LeastIntense },

    // relative thresholding to the base peak, keeping the most intense peaks
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 3 4 5", "10 20 30 20 10", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 0.1, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "2 3 4", "20 30 20", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 0.34, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "2 3 4", "20 30 20", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 0.65, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "3", "30", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 0.67, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "", "", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 1.0, ThresholdFilter::Orientation_MostIntense },

    // relative thresholding to the base peak, keeping the least intense peaks
    { "1 2 3 4 5", "10 20 30 20 10", "", "", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 0.1, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "", "", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 0.32, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 5", "10 10", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 0.34, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 4 5", "10 20 20 10", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 0.67, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 4 5", "10 20 20 10", ThresholdFilter::ThresholdingBy_FractionOfBasePeakIntensity, 1.0, ThresholdFilter::Orientation_LeastIntense },

    // relative thresholding to total intensity, keeping the most intense peaks
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 3 4 5", "10 20 30 20 10", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.1, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "2 3 4", "20 30 20", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.12, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "2 3 4", "20 30 20", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.21, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "3", "30", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.23, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "", "", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.34, ThresholdFilter::Orientation_MostIntense },

    // relative thresholding to total intensity, keeping the least intense peaks
    { "1 2 3 4 5", "10 20 30 20 10", "", "", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.1, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 5", "10 10", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.12, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 5", "10 10", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.21, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 4 5", "10 20 20 10", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.23, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 3 4 5", "10 20 30 20 10", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensity, 0.34, ThresholdFilter::Orientation_LeastIntense },

    // threshold against cumulative total intensity fraction, keeping the most intense peaks (ties are included)
    // intensities:     12  2   2   1   1   1   1   0   0  (TIC 20)
    // cumulative:      12  14  16  17  18  19  20  20  20
    // fraction:        .60 .70 .80 .85 .90 .95 1.0 1.0 1.0
    // at threshold 1.0 ---------------------------^ cut here
    // at threshold .99 ---------------------------^ cut here
    // at threshold .90 ---------------------------^ cut here
    // at threshold .80 -----------^ cut here
    // at threshold .65 -----------^ cut here
    // at threshold .60 ---^ cut here
    // at threshold .15 ---^ cut here
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "2 3 4 6 7 8 9", "1 2 1 1 2 12 1", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 1.0, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "2 3 4 6 7 8 9", "1 2 1 1 2 12 1", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.99, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "2 3 4 6 7 8 9", "1 2 1 1 2 12 1", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.90, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "3 7 8", "2 2 12", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.80, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "3 7 8", "2 2 12", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.65, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "8", "12", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.60, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "8", "12", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.15, ThresholdFilter::Orientation_MostIntense },

    // threshold against cumulative total intensity fraction, keeping the least intense peaks (ties are included)
    // intensities:     0   0   1   1   1   1   2   2   12 (TIC 20)
    // cumulative:      0   0   1   2   3   4   6   8   20
    // fraction:        0   0   .05 .10 .15 .20 .30 .40 1.0
    // at threshold 1.0 -----------------------------------^ cut here
    // at threshold .45 -----------------------------------^ cut here
    // at threshold .40 -------------------------------^ cut here
    // at threshold .35 -------------------------------^ cut here
    // at threshold .25 -------------------------------^ cut here
    // at threshold .20 -----------------------^ cut here
    // at threshold .01 -----------------------^ cut here
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 1.0, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.45, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "1 2 3 4 5 6 7 9", "0 1 2 1 0 1 2 1", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.40, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "1 2 3 4 5 6 7 9", "0 1 2 1 0 1 2 1", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.35, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "1 2 3 4 5 6 7 9", "0 1 2 1 0 1 2 1", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.25, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "1 2 4 5 6 9", "0 1 1 0 1 1", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.20, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5 6 7 8 9", "0 1 2 1 0 1 2 12 1", "1 2 4 5 6 9", "0 1 1 0 1 1", ThresholdFilter::ThresholdingBy_FractionOfTotalIntensityCutoff, 0.15, ThresholdFilter::Orientation_LeastIntense },

    // keep the <threshold> most intense points, excluding ties
    { "1 2 3 4 5", "10 20 30 20 10", "3", "30", ThresholdFilter::ThresholdingBy_Count, 1, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "3", "30", ThresholdFilter::ThresholdingBy_Count, 2, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "2 3 4", "20 30 20", ThresholdFilter::ThresholdingBy_Count, 3, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "2 3 4", "20 30 20", ThresholdFilter::ThresholdingBy_Count, 4, ThresholdFilter::Orientation_MostIntense },

    // keep the <threshold> least intense points, excluding ties
    { "1 2 3 4 5", "10 20 30 20 10", "", "", ThresholdFilter::ThresholdingBy_Count, 1, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 5", "10 10", ThresholdFilter::ThresholdingBy_Count, 2, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 5", "10 10", ThresholdFilter::ThresholdingBy_Count, 3, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 4 5", "10 20 20 10", ThresholdFilter::ThresholdingBy_Count, 4, ThresholdFilter::Orientation_LeastIntense },

    // keep the <threshold> most intense points, including ties
    { "1 2 3 4 5", "10 20 30 20 10", "3", "30", ThresholdFilter::ThresholdingBy_CountAfterTies, 1, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "2 3 4", "20 30 20", ThresholdFilter::ThresholdingBy_CountAfterTies, 2, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "2 3 4", "20 30 20", ThresholdFilter::ThresholdingBy_CountAfterTies, 3, ThresholdFilter::Orientation_MostIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 3 4 5", "10 20 30 20 10", ThresholdFilter::ThresholdingBy_CountAfterTies, 4, ThresholdFilter::Orientation_MostIntense },

    // keep the <threshold> least intense points, including ties
    { "1 2 3 4 5", "10 20 30 20 10", "1 5", "10 10", ThresholdFilter::ThresholdingBy_CountAfterTies, 1, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 5", "10 10", ThresholdFilter::ThresholdingBy_CountAfterTies, 2, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 4 5", "10 20 20 10", ThresholdFilter::ThresholdingBy_CountAfterTies, 3, ThresholdFilter::Orientation_LeastIntense },
    { "1 2 3 4 5", "10 20 30 20 10", "1 2 4 5", "10 20 20 10", ThresholdFilter::ThresholdingBy_CountAfterTies, 4, ThresholdFilter::Orientation_LeastIntense }
};

const size_t testThresholdersSize = sizeof(testThresholders) / sizeof(TestThresholder);

void testIntensityThresholding()
{
    // default msLevelsToThreshold should include all levels
    for (size_t i=0; i < testThresholdersSize; ++i)
    {
        SpectrumListSimple* sl = new SpectrumListSimple;
        SpectrumListPtr originalList(sl);
        SpectrumPtr s(new Spectrum);
        s->set(MS_ms_level, 2);
        sl->spectra.push_back(s);

        TestThresholder& t = testThresholders[i];

        vector<double> inputMZArray = parseDoubleArray(t.inputMZArray);
        vector<double> inputIntensityArray = parseDoubleArray(t.inputIntensityArray);
        s->setMZIntensityArrays(inputMZArray, inputIntensityArray, MS_number_of_detector_counts);

        SpectrumDataFilterPtr pFilter = SpectrumDataFilterPtr(new ThresholdFilter(t.byType, t.threshold, t.orientation));
        SpectrumListPtr thresholder(new SpectrumList_PeakFilter(originalList, pFilter));

        vector<double> outputMZArray = parseDoubleArray(t.outputMZArray);
        vector<double> outputIntensityArray = parseDoubleArray(t.outputIntensityArray);

        SpectrumPtr thresholdedSpectrum = thresholder->spectrum(0, true);
        //if (os_) cout << s1->defaultArrayLength << ": " << s1->getMZArray()->data << " " << s1->getIntensityArray()->data << endl;
        unit_assert(thresholdedSpectrum->defaultArrayLength == outputMZArray.size());
        for (size_t i=0; i < outputMZArray.size(); ++i)
        {
            unit_assert(thresholdedSpectrum->getMZArray()->data[i] == outputMZArray[i]);
            unit_assert(thresholdedSpectrum->getIntensityArray()->data[i] == outputIntensityArray[i]);
        }
    }

    // test that msLevelsToThreshold actually works
    for (size_t i=0; i < testThresholdersSize; ++i)
    {
        SpectrumListSimple* sl = new SpectrumListSimple;
        SpectrumListPtr originalList(sl);
        SpectrumPtr s(new Spectrum);
        s->set(MS_ms_level, 1);
        sl->spectra.push_back(s);

        TestThresholder& t = testThresholders[i];

        vector<double> inputMZArray = parseDoubleArray(t.inputMZArray);
        vector<double> inputIntensityArray = parseDoubleArray(t.inputIntensityArray);
        s->setMZIntensityArrays(inputMZArray, inputIntensityArray, MS_number_of_detector_counts);

        SpectrumDataFilterPtr pFilter = SpectrumDataFilterPtr(new ThresholdFilter(t.byType, t.threshold, t.orientation, IntegerSet(2)));
        SpectrumListPtr thresholder(new SpectrumList_PeakFilter(originalList, pFilter));

        SpectrumPtr unthresholdedSpectrum = thresholder->spectrum(0, true);
        //if (os_) cout << s1->defaultArrayLength << ": " << s1->getMZArray()->data << " " << s1->getIntensityArray()->data << endl;
        unit_assert(unthresholdedSpectrum->defaultArrayLength == inputMZArray.size());
        for (size_t i=0; i < inputMZArray.size(); ++i)
        {
            unit_assert(unthresholdedSpectrum->getMZArray()->data[i] == inputMZArray[i]);
            unit_assert(unthresholdedSpectrum->getIntensityArray()->data[i] == inputIntensityArray[i]);
        }
    }
}

////////////////////////////////////////////////////////////////////////////
//  Markey Deisotoper test
////////////////////////////////////////////////////////////////////////////

struct TestDeisotoper
{
    // space-delimited doubles
    const char* inputMZArray;
    const char* inputIntensityArray;
    const char* outputMZArray;
    const char* outputIntensityArray;

    MZTolerance tol;
    bool hires;
};

TestDeisotoper TestDeisotopers[] =
{
    // Markey method
    {   
        "101 102 103 104 105", 
        "10 20 30 20 10", 
        "101 102 103", 
        "10 20 30", 
        0.5,
        false
    },
};

const size_t testDeisotopersSize = sizeof(TestDeisotopers) / sizeof(TestDeisotoper);

void testDeisotoping()
{
    for (size_t i=0; i < testDeisotopersSize; ++i)
    {

        SpectrumListSimple* sl = new SpectrumListSimple;
        SpectrumListPtr originalList(sl);
        SpectrumPtr s(new Spectrum);
        sl->spectra.push_back(s);

        TestDeisotoper& t = TestDeisotopers[i];

        vector<double> inputMZArray = parseDoubleArray(t.inputMZArray);
        vector<double> inputIntensityArray = parseDoubleArray(t.inputIntensityArray);
        s->setMZIntensityArrays(inputMZArray, inputIntensityArray, MS_number_of_detector_counts);
        s->set(MS_MSn_spectrum);
        s->set(MS_ms_level, 2);
        s->precursors.resize(1);
        s->precursors[0].activation.set(MS_electron_transfer_dissociation);
        s->precursors[0].selectedIons.resize(1);
        s->precursors[0].selectedIons[0].set(MS_selected_ion_m_z, PRECURSOR_MZ, MS_m_z);
        s->precursors[0].selectedIons[0].set(MS_charge_state, PRECURSOR_CHARGE);

        SpectrumDataFilterPtr pFilter = SpectrumDataFilterPtr(new MS2Deisotoper(MS2Deisotoper::Config(t.tol, t.hires)));
        SpectrumListPtr deisotopedList(new SpectrumList_PeakFilter(originalList, pFilter));

        vector<double> outputMZArray = parseDoubleArray(t.outputMZArray);
        vector<double> outputIntensityArray = parseDoubleArray(t.outputIntensityArray);

        SpectrumPtr deisotopedSpectrum = deisotopedList->spectrum(0, true);
        unit_assert(deisotopedSpectrum->defaultArrayLength == outputMZArray.size());
        for (size_t i=0; i < outputMZArray.size(); ++i)
        {
            unit_assert(deisotopedSpectrum->getMZArray()->data[i] == outputMZArray[i]);
            unit_assert(deisotopedSpectrum->getIntensityArray()->data[i] == outputIntensityArray[i]);
        }
    }
}

////////////////////////////////////////////////////////////////////////////
//  Moving Window MS2 Denoise test
////////////////////////////////////////////////////////////////////////////

struct TestMS2Denoise
{
    // space-delimited doubles
    const char* inputMZArray;
    const char* inputIntensityArray;
    const char* outputMZArray;
    const char* outputIntensityArray;

    double precursorMass;
    int precursorCharge;
    double windowSize;
    int keepTopN;
    bool relaxLowMass;
};

TestMS2Denoise TestMS2DenoiseArr[] =
{
    {   // basic test
        "101 102 103 104 105", 
        "10 20 30 20 10", 
        "102 103 104", 
        "20 30 20", 
        500.0,
        2,
        10.0,
        3,
        false
    },
    {   // verify removal of precursor and masses above parent mass minus glycine
        "101 102 103 104 105 500 945", 
        "10 20 30 20 10 10 10", 
        "102 103 104", 
        "20 30 20", 
        500.0,
        2,
        10.0,
        3,
        false
    },
};

const size_t testMS2DenoiseSize = sizeof(TestMS2DenoiseArr) / sizeof(TestMS2Denoise);

void testMS2Denoising()
{
    for (size_t i=0; i < testMS2DenoiseSize; ++i)
    {

        SpectrumListSimple* sl = new SpectrumListSimple;
        SpectrumListPtr originalList(sl);
        SpectrumPtr s(new Spectrum);
        sl->spectra.push_back(s);

        TestMS2Denoise& t = TestMS2DenoiseArr[i];

        vector<double> inputMZArray = parseDoubleArray(t.inputMZArray);
        vector<double> inputIntensityArray = parseDoubleArray(t.inputIntensityArray);
        s->setMZIntensityArrays(inputMZArray, inputIntensityArray, MS_number_of_detector_counts);
        s->set(MS_MSn_spectrum);
        s->set(MS_ms_level, 2);
        s->precursors.resize(1);
        s->precursors[0].activation.set(MS_electron_transfer_dissociation);
        s->precursors[0].selectedIons.resize(1);
        s->precursors[0].selectedIons[0].set(MS_selected_ion_m_z, t.precursorMass, MS_m_z);
        s->precursors[0].selectedIons[0].set(MS_charge_state, t.precursorCharge);

        SpectrumDataFilterPtr pFilter = SpectrumDataFilterPtr(new MS2NoiseFilter(MS2NoiseFilter::Config(t.keepTopN, t.windowSize, t.relaxLowMass)));
        SpectrumListPtr filteredList(new SpectrumList_PeakFilter(originalList, pFilter));

        vector<double> outputMZArray = parseDoubleArray(t.outputMZArray);
        vector<double> outputIntensityArray = parseDoubleArray(t.outputIntensityArray);

        SpectrumPtr filteredSpectrum = filteredList->spectrum(0, true);
        unit_assert(filteredSpectrum->defaultArrayLength == outputMZArray.size());
        for (size_t i=0; i < outputMZArray.size(); ++i)
        {
            unit_assert(filteredSpectrum->getMZArray()->data[i] == outputMZArray[i]);
            unit_assert(filteredSpectrum->getIntensityArray()->data[i] == outputIntensityArray[i]);
        }
    }
    
}

void testZeroSamplesFilter() {
    const char* RawX =
    "300.000066203793 300.000611626572 300.001157051333 300.001702478078 300.058437690186 300.058983325233 300.059528962264 300.06007460128 300.06062024228 300.061165885264 300.061711530233 300.062257177186 300.062802826123 300.063348477046 300.063894129952 300.064439784843 300.064985441719 300.065531100579 300.066076761424 301.055887660805 301.056436929468 301.056986200136 301.057535472809 301.058084747485 301.058634024166 301.059183302851 301.059732583541 301.060281866235 301.060831150933 301.061380437635 301.061929726342 301.062479017053 301.063028309769 301.063577604489 311.869088211176 311.869677645283 311.870267081618 311.870856520182 311.871445960974 311.872035403993 311.872624849241 311.873214296717 311.873803746421 311.874393198353 311.874982652514 311.875572108902 311.876161567519 311.876751028364 311.877340491437 311.877929956739 311.878519424268 311.879108894026 311.879698366013 311.880287840227 311.88087731667 311.881466795341 311.882056276241 315.73174362051 315.732347745926 315.732951873654 315.733556003694 315.734160136047 315.734764270711 315.735368407687 315.735972546974 315.736576688574 315.737180832486 315.73778497871 315.738389127246 316.901416544052 316.902025153901 316.902633766087 316.90324238061 316.903850997471 316.90445961667 316.905068238207 316.905676862081 316.906285488293 316.906894116843 316.907502747731 316.908111380957 316.90872001652 316.909328654421 326.293591849569 326.294237069432 326.294882291847 326.295527516814 326.296172744332 326.296817974402 326.297463207024 326.298108442198 326.298753679923 326.299398920201 326.30004416303 327.074882186811 327.075530500256 327.076178816272 327.076827134858 327.077475456014 327.07812377974 327.078772106036 327.079420434903 327.080068766339 327.080717100346 327.081365436923 327.082013776071 327.082662117789 327.083310462077 327.083958808935 341.007109159311 341.007813880848 341.008518605298 341.00922333266 341.009928062936 341.010632796124 341.011337532225 341.012042271238 341.012747013165 "
    "341.013451758004 341.014156505757 341.014861256422 341.01556601 341.016270766491 341.016975525895 341.017680288212 341.018385053442 341.019089821585 341.019794592642 341.020499366611 341.021204143493 341.021908923288 341.022613705997 341.023318491618 341.024023280153 341.024728071601 342.01359244987 342.014301337525 342.015010228119 342.015719121651 342.016428018122 342.017136917532 342.01784581988 342.018554725167 342.019263633392 342.019972544557 342.02068145866 342.021390375702 342.022099295682 342.022808218602 342.02351714446 342.873501341248 342.874213798035 342.874926257782 342.87563872049 342.876351186159 342.877063654789 342.87777612638 342.878488600932 342.879201078445 342.879913558918 342.880626042353 342.881338528749 344.97306118134 344.9737823902 344.974503602076 344.975224816967 344.975946034874 344.976667255797 344.977388479735 344.978109706689 344.978830936658 344.979552169644 344.980273405644 344.980994644661 344.981715886693 355.063935356091 355.064699374529 355.065463396255 355.066227421269 355.066991449572 355.067755481162 355.06851951604 355.069283554207 355.070047595661 355.070811640404 355.071575688435 355.072339739754 355.073103794361 355.073867852257 355.074631913441 355.075395977913 355.076160045673 355.076924116722 355.077688191059 355.078452268685 356.064553240471 356.065321571195 356.066089905234 356.06685824259 356.067626583261 356.068394927249 356.069163274552 356.069931625172 356.070699979107 356.071468336359 356.072236696926 356.07300506081 356.073773428009 356.074541798525 356.075310172357 356.076078549505 356.076846929969 356.07761531375 356.078383700846 356.079152091259 356.079920484988 356.080688882034 357.062327559534 357.063100202372 357.063872848555 357.064645498081 357.065418150951 357.066190807165 357.066963466723 357.067736129625 357.068508795871 357.069281465461 357.070054138395 357.070826814673 357.071599494295 357.072372177261 357.073144863571 357.073917553225 357.074690246224 357.075462942566 357.076235642253 "
    "357.077008345284 357.077781051659 357.078553761379 357.079326474442 357.08009919085 359.023183355092 359.023964507385 359.024745663078 359.02552682217 359.026307984661 359.027089150551 359.027870319841 359.028651492531 359.029432668619 359.030213848107 359.030995030994 359.031776217281 359.032557406967 359.033338600053 359.034119796538 359.034900996422 359.035682199706 360.023491974674 360.024277485921 360.025063000597 360.0258485187 360.02663404023 360.027419565189 360.028205093575 360.028990625389 360.029776160632 360.030561699301 360.031347241399 360.032132786925 360.032918335879 360.03370388826 360.03448944407 361.021491400285 361.02228127251 361.023071148191 361.023861027329 361.024650909923 361.025440795974 361.026230685481 361.027020578444 361.027810474864 361.02860037474 361.029390278072 361.030180184861 361.030970095107 361.031760008809 371.095690561049 371.096525130684 371.097359704072 371.098194281215 371.099028862111 371.099863446762 371.100698035166 371.101532627324 371.102367223236 371.103201822902 371.104036426322 371.104871033497 371.105705644425 371.106540259107 371.107374877543 371.108209499734 371.109044125678 371.109878755377 371.11071338883 372.078066820464 372.078905814551 372.079744812421 372.080583814075 372.081422819513 372.082261828735 372.083100841741 372.08393985853 372.084778879103 372.08561790346 372.086456931601 372.087295963526 372.095686490895 372.096525564444 372.097364641778 372.098203722896 372.099042807799 372.099881896485 372.100720988956 372.101560085211 372.102399185251 372.103238289075 372.104077396683 372.104916508076 372.105755623254 372.106594742216 372.107433864962 372.108272991493 373.076623237254 373.077466740653 373.078310247866 373.079153758894 373.079997273735 373.080840792391 373.081684314862 373.082527841147 373.083371371246 373.084214905159 373.085058442887 373.08590198443 373.086745529787 373.101086384495 373.101929998517 373.102773616354 373.103617238007 373.104460863474 373.105304492756 373.106148125853 "
    "373.106991762765 373.107835403493 373.108679048035 373.109522696393 373.110366348566 373.111210004555 415.030543316388 415.031587197383 415.032631083628 415.033674975125 415.034718871873 415.035762773872 415.036806681123 415.037850593625 415.038894511378 415.039938434383 415.040982362639 415.042026296147 415.043070234906 415.044114178917 415.045158128179 415.046202082693 415.047246042459 416.004675140758 416.005723927765 416.006772720059 416.007821517642 416.008870320513 416.009919128672 416.01096794212 416.012016760856 416.01306558488 416.014114414193 416.015163248794 416.016212088684 416.030896402465 416.031945321688 416.032994246201 416.034043176002 416.035092111093 416.036141051473 416.037189997143 416.038238948102 416.03928790435 416.040336865888 416.041385832715 416.042434804832 416.043483782239 416.044532764935 416.04558175292 416.046630746196 416.047679744761 417.027651705639 417.028705657034 417.029759613757 417.030813575807 417.031867543184 417.032921515889 417.033975493922 417.035029477282 417.036083465969 417.037137459984 417.038191459327 417.039245463997 417.040299473995 417.041353489321 426.357976566382 426.359078206359 426.360179852029 426.361281503392 426.362383160449 426.363484823198 426.36458649164 426.365688165776 426.366789845604 426.367891531126 426.368993222341 428.935877697296 428.936992699323 428.938107707146 428.939222720767 428.940337740184 428.941452765398 428.94256779641 428.943682833218 428.944797875823 428.945912924226 428.947027978426 429.083108101101 429.0842238687 429.085339642101 429.086455421305 429.087571206312 429.088686997122 429.089802793735 429.090918596151 429.09203440437 429.093150218392 429.094266038217 429.095381863845 429.096497695277 429.097613532512 429.09872937555 429.099845224392 429.100961079037 429.102076939486 429.134439417209 429.135555451783 429.136671492162 429.137787538345 429.138903590334 429.140019648128 429.141135711726 429.14225178113 429.143367856339 429.144483937353 429.145600024173 429.146716116797 "
    "429.85101025753 429.852130022341 429.853249792985 429.854369569464 429.855489351777 429.856609139924 429.857728933905 429.858848733721 429.859968539371 429.861088350855 429.862208168173 429.863327991326 429.864447820313 429.865567655135 429.866687495792 429.867807342283 429.868927194608 429.870047052768 429.871166916764 429.872286786593 429.873406662258 429.874526543758 429.875646431092 429.876766324261 429.877886223266 430.081805059062 430.082926026641 430.084047000064 430.08516797933 430.086288964439 430.087409955392 430.088530952189 430.089651954829 430.090772963313 430.091893977641 430.093014997813 430.094136023828 430.095257055687 430.09637809339 430.097499136937 430.098620186328 430.099741241563 430.100862302643 430.101983369566 431.078404724764 431.079530893455 431.08065706803 431.081783248489 431.082909434832 431.08403562706 431.085161825172 431.086288029168 431.087414239049 431.088540454814 431.089666676463 431.090792903997 431.091919137416 431.093045376719 431.094171621907 431.09529787298 431.096424129938 431.09755039278 431.098676661507 431.099802936119 432.079633824813 432.080765230898 432.081896642908 432.083028060844 432.084159484704 432.085290914491 432.086422350202 432.087553791839 432.088685239402 432.08981669289 432.090948152304 432.092079617643 432.093211088908 444.61606065115 444.617258663358 444.618456682022 444.619654707142 444.620852738719 444.622050776751 444.62324882124 444.624446872186 444.625644929587 444.626842993445 444.62804106376 444.629239140531 444.630437223759 445.111389571898 445.112590254911 445.113790944402 445.114991640371 445.116192342817 445.117393051741 445.118593767143 445.119794489023 445.120995217382 445.122195952218 445.123396693532 445.124597441324 445.125798195595 445.126998956343 445.128199723571 445.129400497276 445.13060127746 446.092099627601 446.093305607359 446.094511593638 446.095717586437 446.096923585757 446.098129591598 446.099335603959 446.100541622841 446.101747648245 446.102953680169 446.104159718614 "
    "446.10536576358 446.106571815068 446.107777873076 446.108983937606 446.110190008657 446.111396086229 446.112602170323 446.113808260938 446.115014358075 446.116220461733 446.117426571913 446.118632688614 446.119838811837 446.121044941582 446.122251077849 446.123457220638 446.124663369948 446.12586952578 446.127075688135 446.128281857011 446.12948803241 446.130694214331 446.131900402774 446.133106597739 447.092888369096 447.09409976606 447.095311169589 447.096522579682 447.09773399634 447.098945419563 447.100156849351 447.101368285703 447.10257972862 447.103791178103 447.10500263415 447.106214096762 447.10742556594 447.108637041682 447.10984852399 447.111060012863 447.112271508302 447.113483010306 447.114694518875 447.115906034009 447.11711755571 447.118329083975 447.119540618807 447.120752160204 447.121963708167 447.123175262696 447.12438682379 447.125598391451 447.126809965677 447.128021546469 447.129233133828 447.130444727752 447.131656328243 449.794778250235 449.796004332987 449.797230422424 449.798456518544 449.799682621349 449.800908730839 449.802134847013 449.803360969872 449.804587099415 449.805813235643 449.807039378556 449.808265528153 476.291926493584 476.293301287096 476.294676088545 476.296050897931 476.297425715253 476.298800540512 476.300175373707 476.30155021484 476.30292506391 476.304299920917 476.305674785861 476.307049658742 487.815419569054 487.81686169142 487.818303822312 487.819745961732 487.821188109678 487.822630266151 487.824072431152 487.825514604679 487.826956786733 487.828398977315 487.829841176424 489.047206755368 489.048656169973 489.050105593169 489.051555024957 489.053004465336 489.054453914307 489.05590337187 489.057352838025 489.058802312771 489.06025179611 489.06170128804 489.063150788563 489.064600297678 489.066049815385 489.067499341685 494.497639238354 494.499121140501 494.500603051529 494.50208497144 494.503566900233 494.505048837908 494.506530784465 494.508012739904 494.509494704226 494.510976677431 494.512458659517 "
    "503.097384350016 503.098918243574 503.100452146485 503.10198605875 503.103519980368 503.10505391134 503.106587851665 503.108121801345 503.109655760378 503.111189728765 503.112723706507 503.114257693603 503.115791690052 503.117325695857 503.118859711015 503.120393735528 503.121927769396 503.123461812619 503.124995865196 504.057895208494 504.059434964655 504.060974730222 504.062514505197 504.064054289579 504.065594083369 504.067133886566 504.06867369917 504.070213521182 504.071753352602 504.073293193429 504.097931926196 504.099471926969 504.101011937151 504.102551956742 504.104091985743 504.105632024154 504.107172071974 504.108712129204 504.110252195844 504.111792271893 504.113332357353 504.114872452223 504.116412556503 504.117952670193 504.119492793294 504.121032925805 504.122573067726 504.124113219059 504.136434768509 504.137975004541 504.139515249984 504.141055504838 504.142595769105 504.144136042783 504.145676325873 504.147216618374 504.148756920288 504.150297231614 504.151837552352 504.153377882502 505.094736580555 505.096282677762 505.097828784435 505.099374900573 505.100921026176 505.102467161245 505.104013305779 505.105559459779 505.107105623245 505.108651796177 505.110197978574 505.111744170438 505.113290371768 505.114836582564 505.116382802826 505.117929032555 505.11947527175 505.121021520412 505.12256777854 506.095491214212 506.097043444127 506.098595683563 506.100147932522 506.101700191001 506.103252459003 506.104804736527 506.106357023573 506.107909320141 506.109461626231 506.111013941844 506.112566266978 506.114118601636 517.627784100423 517.62940787711 517.631031663984 517.632655461045 517.634279268294 517.635903085731 517.637526913356 517.639150751169 517.64077459917 517.642398457359 517.644022325736 519.117811946194 519.11944508466 519.121078233402 519.122711392419 519.124344561713 519.125977741282 519.127610931128 519.129244131249 519.130877341647 519.132510562322 519.134143793272 519.1357770345 519.137410286004 519.139043547785 519.140676819843 "
    "519.142310102177 519.143943394789 519.145576697678 519.147210010844 519.148843334287 519.150476668008 519.152110012007 519.153743366283 520.107741759837 520.109381132867 520.111020516231 520.11265990993 520.114299313963 520.115938728332 520.117578153035 520.119217588074 520.120857033448 520.122496489157 520.124135955201 520.125775431581 520.130693922735 520.132333440458 520.133972968517 520.135612506911 520.137252055643 520.13889161471 520.140531184113 520.142170763854 520.14381035393 520.145449954343 520.147089565093 520.14872918618 520.150368817604 520.152008459365 520.153648111463 520.155287773898 521.127785814089 521.129431623767 521.131077443839 521.132723274308 521.134369115172 521.136014966432 521.137660828088 521.13930670014 521.140952582588 521.142598475432 521.144244378673 521.14589029231 521.147536216343 531.114696350534 531.11640584546 531.118115351391 531.119824868327 531.121534396267 531.123243935212 531.124953485163 531.126663046119 531.12837261808 531.130082201046 531.131791795019 531.179664894929 531.181374808109 531.183084732298 531.184794667495 531.186504613701 531.188214570917 531.189924539141 531.191634518375 531.193344508618 531.195054509871 531.196764522133 531.198474545405 539.793121824335 539.79488764215 539.796653471517 539.798419312438 539.800185164913 539.80195102894 539.803716904521 539.805482791656 539.807248690344 539.809014600586 539.810780522383 544.657227988968 544.659025773986 544.660823570872 544.662621379626 544.664419200249 544.66621703274 544.6680148771 544.669812733329 544.671610601426 544.673408481392 544.675206373228 544.677004276933 544.678802192507 544.68060011995 544.682398059264 544.684196010447 544.685993973499 544.687791948422 544.689589935214 544.691387933877 564.063114662872 564.065042838848 564.066971028006 564.068899230347 564.07082744587 564.072755674577 564.074683916467 564.076612171539 564.078540439795 564.080468721235 564.082397015858 564.084325323664 564.086253644655 577.113045862246 577.115064289413 "
    "577.1170827307 577.119101186105 577.12111965563 577.123138139273 577.125156637036 577.127175148919 577.129193674921 577.131212215043 577.133230769285 577.135249337647 577.137267920129 577.139286516732 577.141305127455 577.143323752299 578.095668254573 578.09769356096 578.099718881538 578.101744216308 578.103769565269 578.105794928421 578.107820305765 578.1098456973 578.111871103028 578.113896522947 578.115921957059 578.117947405363 578.119972867859 578.121998344549 578.124023835431 578.126049340506 578.128074859774 578.130100393235 578.13212594089 578.134151502738 578.13617707878 578.138202669016 578.140228273445 578.142253892069 578.144279524888 578.1463051719 579.112127291178 579.114159725986 579.116192175059 579.118224638399 579.120257116005 579.122289607877 579.124322114016 579.126354634422 579.128387169095 579.130419718034 579.132452281241 579.134484858716 579.136517450457 579.138550056467 579.140582676744 579.142615311289 593.146836106552 593.148968246848 593.151100402472 593.153232573424 593.155364759706 593.157496961317 593.159629178256 593.161761410526 593.163893658125 593.166025921053 593.168158199311 593.1702904929 593.172422801818 593.174555126067 594.334675871054 594.336816559592 594.338957263551 594.341097982931 594.343238717732 594.345379467954 594.347520233598 594.349661014664 594.351801811151 594.35394262306 594.356083450392 594.358224293146 602.991717202929 602.993920708033 602.996124229242 602.998327766555 603.000531319973 603.002734889497 603.004938475125 603.007142076859 603.009345694699 603.011549328644 603.013752978696 603.015956644853 603.018160327117 603.020364025487 603.022567739964 603.024771470548 603.026975217238 647.653798614949 647.656340625328 647.65888265566 647.661424705948 647.66396677619 647.666508866388 647.669050976542 647.671593106651 647.674135256716 647.676677426738 647.679219616716 647.681761826651 647.684304056543 647.686846306392 647.689388576198 647.691930865962 647.694473175684 647.697015505364 647.699557855003 "
    "651.129472007141 651.132041374488 651.134610762114 651.137180170017 651.139749598198 651.142319046658 651.144888515396 651.147458004413 651.150027513709 651.152597043284 651.155166593139 651.157736163274 651.160305753689 651.162875364383 651.165444995359 651.168014646615 652.130488515969 652.133065789459 652.135643083321 652.138220397554 652.140797732159 652.143375087136 652.145952462485 652.148529858207 652.151107274301 652.153684710768 652.156262167609 652.158839644823 652.16141714241 652.163994660372 652.166572198708 652.169149757418 652.594726156514 652.597307100729 652.599888065358 652.602469050403 652.605050055862 652.607631081738 652.610212128029 652.612793194736 652.615374281859 652.617955389399 652.620536517356 653.126832071549 653.129417226335 653.132002401586 653.134587597302 653.137172813482 653.139758050129 653.142343307241 653.14492858482 653.147513882864 653.150099201375 653.152684540353 653.155269899798 653.157855279709 653.160440680089 653.163026100936 660.922514101737 660.925161337446 660.927808594361 660.930455872483 660.933103171812 660.935750492348 660.938397834091 660.941045197042 660.943692581201 660.946339986568 660.948987413144 660.951634860928 660.954282329921 660.983405888706 660.986053612226 660.988701356959 660.991349122903 660.993996910061 660.996644718431 660.999292548015 661.001940398812 661.004588270823 661.007236164048 661.009884078488 661.012532014142 664.411346114577 664.414021372197 664.41669665136 664.419371952069 664.422047274321 664.424722618119 664.427397983461 664.430073370349 664.432748778782 664.435424208761 664.438099660286 664.440775133357 664.443450627975 664.44612614414 664.448801681852 664.451477241112 664.454152821919 664.456828424273 664.459504048176 664.462179693628 664.464855360628 667.159443330433 667.162140764336 667.164838220052 667.16753569758 667.170233196922 667.172930718076 667.175628261044 667.178325825826 667.181023412421 667.183721020831 667.186418651055 667.189116303095 667.191813976949 "
    "667.194511672619 667.197209390104 667.199907129405 668.145457833561 668.148163246605 668.150868681559 668.153574138422 668.156279617196 668.158985117879 668.161690640472 668.164396184977 668.167101751392 668.169807339718 668.172512949956 668.175218582105 668.177924236166 668.180629912139 668.183335610025 668.186041329824 668.188747071536 668.19145283516 668.194158620699 668.196864428151 668.199570257517 670.158915513485 670.161637256666 670.164359021956 670.167080809353 670.16980261886 670.172524450475 670.175246304198 670.177968180032 670.180690077974 670.183411998027 670.186133940189 670.188855904462 670.191577890846 677.272020770102 677.274800597507 677.277580447732 677.280360320776 677.283140216641 677.285920135325 677.28870007683 677.291480041156 677.294260028303 677.297040038272 677.299820071062 677.302600126674 677.305380205108 677.308160306364 677.310940430444 677.313720577346 677.316500747072 677.319280939622 677.322061154995 677.324841393193 677.327621654214 677.330401938061 677.333182244733 677.33596257423 684.185792158615 684.188629030244 684.191465925398 684.194302844078 684.197139786283 684.199976752015 684.202813741274 684.205650754059 684.208487790372 684.211324850212 684.21416193358 684.216999040475 684.2198361709 684.222673324852 684.225510502334 685.302497766186 685.305343905889 685.308190069232 685.311036256217 685.313882466843 685.316728701111 685.319574959021 685.322421240572 685.325267545767 685.328113874604 685.330960227085 685.333806603209 685.336653002977 685.339499426388 685.345192344145 685.34803883849 685.350885356481 685.353731898117 685.356578463399 685.359425052328 685.362271664902 685.365118301123 685.367964960991 685.370811644507 685.37365835167 685.376505082481 686.165961086862 686.168814403222 686.171667743312 686.174521107133 686.177374494685 686.180227905968 686.183081340982 686.185934799728 686.188788282207 686.191641788417 686.19449531836 686.197348872036 699.464091765658 699.467056750447 699.470021760373 699.472986795436 "
    "699.475951855637 699.478916940975 699.481882051452 699.484847187067 699.487812347821 699.490777533714 699.493742744746 699.496707980919 699.499673242231 699.502638528684 699.505603840277 699.508569177012 699.511534538887 699.514499925905 707.540962759248 707.54399661424 707.54703049525 707.550064402278 707.553098335324 707.556132294389 707.559166279473 707.562200290576 707.565234327699 707.568268390842 707.571302480006 707.574336595191 707.577370736396 707.580404903623 707.583439096872 707.586473316143 707.589507561437 707.592541832753 707.595576130092 712.253358999635 712.256433401729 712.259507830364 712.26258228554 712.265656767258 712.268731275519 712.271805810321 712.274880371667 712.277954959555 712.281029573987 712.284104214963 714.637817537752 714.640912559113 714.644007607282 714.647102682261 714.650197784048 714.653292912646 714.656388068053 714.65948325027 714.662578459298 714.665673695137 714.668768957787 714.671864247249 725.142802571441 725.145989253758 725.149175964084 725.152362702418 725.155549468761 725.158736263113 725.161923085476 725.165109935848 725.168296814231 725.171483720624 725.174670655029 725.177857617445 725.181044607873 725.184231626314 725.187418672767 725.190605747233 725.193792849713 725.196979980206 726.144799061961 726.147994557046 726.151190080255 726.15438563159 726.15758121105 726.160776818635 726.163972454346 726.167168118184 726.170363810149 726.17355953024 726.176755278459 726.179951054806 726.183146859281 726.186342691885 726.189538552617 727.133547186777 727.136751390037 727.139955621536 727.143159881275 727.146364169255 727.149568485475 727.152772829936 727.155977202638 727.159181603583 727.162386032769 727.165590490198 727.16879497587 727.171999489785 727.175204031944 727.178408602347 727.181613200995 727.184817827887 727.188022483024 728.144269729676 728.147482846893 728.150695992466 728.153909166398 728.157122368687 728.160335599336 728.163548858343 728.16676214571 728.169975461436 728.173188805523 728.17640217797 "
    "728.179615578778 728.182829007947 728.623337067404 728.626554414027 728.629771789064 728.632989192515 728.63620662438 728.639424084659 728.642641573354 728.645859090464 728.64907663599 728.652294209931 728.65551181229 728.658729443065 739.579801260267 739.583116094462 739.586430958371 739.589745851995 739.593060775335 739.596375728391 739.599690711162 739.603005723651 739.606320765856 739.609635837779 739.61295093942 741.174345003278 741.177674146565 741.18100331976 741.184332522862 741.187661755872 741.190991018791 741.194320311618 741.197649634355 741.200978987002 741.204308369558 741.207637782025 741.210967224403 741.214296696693 741.217626198894 741.220955731007 741.224285293032 742.067626706004 742.070963878878 742.074301081766 742.077638314671 742.080975577592 742.08431287053 742.087650193485 742.090987546457 742.094324929448 742.097662342457 742.100999785484 742.104337258531 742.107674761597 742.111012294683 742.11434985779 742.117687450917 742.121025074066 742.124362727236 742.127700410428 742.131038123643 742.13437586688 742.137713640141 742.141051443425 742.174431127655 742.177769261226 742.181107424826 742.184445618454 742.187783842111 742.191122095798 742.194460379515 742.197798693263 742.201137037042 742.204475410852 742.207813814694 742.211152248568 742.214490712474 742.217829206413 742.221167730386 742.224506284392 742.227844868433 743.1470966977 743.150443586686 743.153790505819 743.157137455098 743.160484434525 743.1638314441 743.167178483823 743.170525553695 743.173872653716 743.177219783887 743.180566944208 743.183914134679 743.187261355301 743.190608606074 743.193955886998 743.197303198075 743.200650539304 743.203997910686 743.20734531222 743.210692743909 743.214040205752 743.217387697749 743.220735219901 743.224082772208 743.227430354671 743.23077796729 744.176009584717 744.179365747906 744.182721941368 744.186078165103 744.18943441911 744.192790703391 744.196147017945 744.199503362773 744.202859737876 744.206216143254 744.209572578908 "
    "744.212929044837 744.216285541042 744.219642067524 744.222998624283 758.202410978174 758.205894849435 758.209378752713 758.212862688007 758.216346655318 758.219830654647 758.223314685994 758.226798749359 758.230282844744 758.233766972148 758.237251131571 758.240735323015 758.24421954648 758.247703801965 758.251188089473 758.254672409002 759.203597733713 759.207090811794 759.210583922019 759.214077064387 759.2175702389 759.221063445557 759.22455668436 759.228049955308 759.231543258401 759.235036593642 759.238529961029 759.242023360563 759.245516792245 759.249010256075 760.200427521877 760.203929778783 760.207432067959 760.210934389405 760.214436743123 760.217939129111 760.221441547372 760.224943997905 760.228446480711 760.231948995789 760.235451543142 760.238954122768 760.24245673467 760.316019040799 760.319522362853 760.323025717192 760.326529103815 760.330032522725 760.33353597392 760.337039457402 760.340542973171 760.344046521228 760.347550101572 760.351053714205 760.354557359126 760.358061036338 760.361564745838 777.189954415057 777.193614963706 777.197275546837 777.200936164451 777.204596816548 777.208257503129 777.211918224194 777.215578979744 777.219239769779 777.2229005943 777.226561453308 777.230222346802 777.233883274784 777.237544237253 799.160490663856 799.164361099883 799.1682315734 799.172102084408 799.175972632907 799.179843218898 799.183713842382 799.187584503358 799.191455201828 799.195325937792 799.19919671125 799.203067522204 799.206938370653 799.210809256599 799.214680180041 799.21855114098 800.16418774129 800.168067905512 800.171948107365 800.175828346849 800.179708623967 800.183588938718 800.187469291103 800.191349681123 800.195230108777 800.199110574067 800.202991076993 800.206871617555 800.210752195754 801.158739593593 801.162629409414 801.166519263006 801.170409154372 801.174299083511 801.178189050424 801.182079055111 801.185969097572 801.18985917781 801.193749295823 801.197639451613 801.20152964518 801.205419876525 801.209310145647 "
    "801.213200452549 810.055165196855 810.059141881054 810.063118604297 810.067095366586 810.07107216792 810.0750490083 810.079025887728 810.083002806202 810.086979763725 810.090956760296 810.094933795916 810.098910870585 810.102887984305 810.106865137076 815.189564537267 815.19359179245 815.197619087424 815.20164642219 815.205673796749 815.209701211101 815.213728665248 815.217756159189 815.221783692925 815.225811266457 815.229838879786 815.233866532911 815.237894225833 815.241921958555 815.245949731074 815.249977543393 815.395005298192 815.3990345835 815.40306390863 815.407093273582 815.411122678358 815.415152122956 815.419181607379 815.423211131626 815.427240695699 815.431270299597 815.435299943322 815.439329626874 816.169358934812 816.17339587673 816.177432858584 816.181469880374 816.1855069421 816.189544043764 816.193581185365 816.197618366904 816.201655588382 816.2056928498 816.209730151157 816.213767492455 816.217804873695 816.221842294876 816.225879756 816.229917257066 816.233954798077 816.237992379031 816.24202999993 816.246067660775 816.250105361565 816.254143102302 817.183885871236 817.187932855536 817.19197987992 817.196026944389 817.200074048944 817.204121193585 817.208168378313 817.212215603128 817.216262868032 817.220310173024 817.224357518104 817.228404903275 817.232452328536 817.236499793888 817.240547299332 817.244594844868 817.248642430496 817.252690056218 818.164426363991 818.16848306611 818.172539808458 818.176596591035 818.180653413842 818.18471027688 818.18876718015 818.192824123651 818.196881107384 818.200938131351 818.204995195551 818.209052299986 818.213109444656 818.21716662956 818.221223854701 818.225281120079 818.229338425694 818.233395771546 818.237453157638 818.241510583968 818.245568050537 818.249625557347 819.18392233678 819.18798915515 819.1920560139 819.19612291303 819.200189852541 819.204256832432 819.208323852706 819.212390913362 819.216458014401 819.220525155824 819.224592337631 819.228659559823 819.2327268224 819.236794125363 "
    "819.240861468713 819.24492885245 820.193731546341 820.197808397262 820.201885288712 820.205962220691 820.210039193201 820.214116206241 820.218193259813 820.222270353916 820.226347488553 820.230424663722 820.234501879425 820.238579135663 832.21883436359 832.223031634984 832.227228948716 832.231426304787 832.235623703196 832.239821143946 832.244018627035 832.248216152466 832.252413720239 832.256611330353 832.260808982811 832.265006677613 832.269204414758 832.273402194249 832.277600016085 832.281797880267 832.285995786796 832.290193735673 832.294391726897 832.298589760471 833.218980432568 833.223187798465 833.227395206852 833.231602657731 833.235810151102 833.240017686965 833.244225265321 833.248432886172 833.252640549516 833.256848255356 833.261056003692 833.265263794525 833.269471627854 833.273679503681 834.221533310184 834.225750807043 834.229968346546 834.234185928695 834.238403553488 834.242621220928 834.246838931015 834.251056683749 834.255274479132 834.259492317163 834.263710197844 834.267928121174 835.218046494203 835.222274073065 835.226501694725 835.230729359182 835.234957066438 835.239184816494 835.243412609349 835.247640445004 835.251868323461 835.256096244719 835.26032420878 835.264552215644 835.268780265312 835.273008357784 835.277236493061 873.174235302306 873.178855854748 873.18347645609 873.188097106335 873.192717805482 873.197338553533 873.201959350488 873.206580196348 873.211201091114 873.215822034786 873.220443027366 873.225064068854 873.229685159251 873.234306298558 873.238927486775 874.17804253512 874.182673717335 874.18730494862 874.191936228976 874.196567558404 874.201198936904 874.205830364477 874.210461841124 874.215093366845 874.219724941642 874.224356565516 874.228988238466 874.233619960494 874.238251731601 874.303101680785 874.307734188164 874.312366744634 874.316999350196 874.321632004851 874.3262647086 874.330897461442 874.335530263379 874.340163114412 874.344796014542 874.349428963769 874.354061962094 875.174876842222 875.179518592457 "
    "875.184160391931 875.188802240643 875.193444138596 875.198086085788 875.202728082222 875.207370127898 875.212012222817 875.216654366978 875.221296560385 875.225938803036 875.230581094934 889.200602342844 889.205394064883 889.210185838565 889.214977663891 889.219769540862 889.224561469479 889.229353449744 889.234145481656 889.238937565216 889.243729700426 889.248521887286 889.253314125797 889.25810641596 889.262898757776 889.267691151245 889.272483596368 889.277276093147 889.282068641582 889.286861241673 889.291653893423 889.296446596831 889.301239351898 889.306032158626 889.310825017014 889.315617927065 889.320410888778 889.325203902155 889.329996967196 889.334790083902 890.02073813417 890.0255386994 890.030339316416 890.03513998522 890.039940705812 890.044741478193 890.049542302364 890.054343178326 890.059144106079 890.063945085625 890.068746116964 890.073547200097 890.078348335025 890.083149521749 890.174381911812 890.179184134627 890.183986409256 890.188788735699 890.193591113957 890.198393544031 890.203196025921 890.207998559629 890.212801145155 890.217603782501 890.222406471667 890.227209212653 890.232012005462 890.236814850093 890.241617746547 890.246420694826 890.25122369493 890.25602674686 890.260829850616 890.2656330062 890.270436213613 890.275239472855 890.280042783927 891.198426546905 891.203239824931 891.208053154948 891.212866536959 891.217679970964 891.222493456964 891.22730699496 891.232120584952 891.236934226942 891.24174792093 891.246561666917 891.251375464905 891.256189314893 891.261003216882 891.265817170875 891.27063117687 891.27544523487 891.280259344875 892.200708915166 892.205533025772 892.210357188546 892.21518140349 892.220005670603 892.224829989886 892.229654361341 892.234478784968 892.239303260768 892.244127788742 892.248952368891 892.253777001215 892.258601685716 892.263426422395 892.268251211251 892.273076052286 893.142398272161 893.147232571559 893.15206692329 893.156901327355 893.161735783755 893.166570292491 893.171404853563 "
    "893.176239466974 893.181074132723 893.185908850811 893.190743621239 893.20041331912 893.205248246574 893.210083226371 893.214918258513 893.219753343 893.224588479834 893.229423669014 893.234258910542 893.239094204419 893.243929550645 893.248764949222 893.25360040015 893.25843590343 893.263271459063 893.26810706705 893.272942727391 894.197514830975 894.202360559179 894.207206339903 894.212052173146 894.21689805891 894.221743997196 894.226589988005 894.231436031336 894.236282127192 894.241128275573 894.24597447648 894.250820729913 894.255667035874 894.260513394364 894.265359805383 894.270206268932 906.234202718878 906.239179781027 906.244156897845 906.249134069332 906.25411129549 906.259088576319 906.26406591182 906.269043301994 906.274020746843 906.278998246366 906.283975800565 906.288953409441 906.293931072995 906.298908791226 907.235692252254 907.240680320926 907.245668444448 907.250656622821 907.255644856045 907.260633144123 907.265621487054 907.270609884839 907.27559833748 907.280586844978 907.285575407332 907.290564024546 907.295552696618 907.30054142355 908.234398692328 908.239397749042 908.244396860787 908.249396027565 908.254395249375 908.25939452622 908.2643938581 908.269393245016 908.274392686968 908.279392183959 908.284391735988 908.289391343056 922.842856257044 922.84801742175 922.853178644185 922.858339924351 922.86350126225 922.86866265788 922.873824111245 922.878985622344 922.884147191179 922.88930881775 922.894470502059 934.042623867136 934.0479110658 934.053198324322 934.058485642702 934.063773020941 934.069060459041 934.074347957002 934.079635514825 934.084923132512 934.090210810063 934.09549854748 934.100786344763 934.106074201913 934.111362118932 962.94815872668 962.953778232548 962.959397804004 962.96501744105 962.970637143686 962.976256911914 962.981876745735 962.987496645149 962.993116610159 962.998736640765 963.004356736968 963.00997689877 963.217969011729 963.223591667127 963.229214388169 963.234837174856 963.240460027188 963.246082945167 "
    "963.251705928794 963.257328978069 963.262952092996 963.268575273574 963.274198519804 963.279821831688 963.285445209227 963.291068652422 963.296692161274 963.302315735784 963.307939375954 963.313563081784 963.319186853277 963.324810690432 963.420425969611 963.426050988895 963.431676073865 963.43730122452 963.442926440862 963.448551722892 963.454177070612 963.459802484022 963.465427963124 963.471053507919 963.476679118408 963.482304794592 964.21420252401 964.219836816216 964.225471174269 964.23110559817 964.236740087921 964.242374643523 964.248009264976 964.253643952283 964.259278705443 964.26491352446 964.270548409332 964.276183360063 964.281818376652 964.287453459101 964.293088607411 964.298723821583 964.304359101619 964.309994447519 964.315629859286 964.321265336919 964.32690088042 964.33253648979 964.338172165031 964.343807906143 964.349443713128 964.355079585987 964.36071552472 964.36635152933 964.467810878442 964.473448134917 964.479085457292 964.484722845568 964.490360299745 964.495997819824 964.501635405808 964.507273057696 964.512910775491 964.518548559193 964.524186408804 964.529824324324 965.201207199106 965.206853032182 965.212498931307 965.218144896483 965.223790927711 965.229437024992 965.235083188328 965.240729417718 965.246375713166 965.252022074672 965.257668502236 965.26331499586 965.268961555546 965.274608181294 965.280254873106 965.285901630983 965.291548454925 965.297195344935 965.302842301012 965.308489323159 965.314136411377 965.319783565667 965.325430786029 965.331078072465 966.212864647697 966.218522322157 966.224180062874 966.22983786985 966.235495743085 966.241153682582 966.246811688342 966.252469760364 966.258127898651 966.263786103204 966.269444374024 966.275102711111 966.280761114469 966.286419584096 966.292078119995 966.297736722167 966.303395390612 966.309054125333 966.31471292633 966.320371793604 966.326030727157 967.215306113877 967.220975534084 967.226645020755 967.232314573891 967.237984193494 967.243653879564 967.249323632103 "
    "967.254993451112 967.260663336592 967.266333288544 967.272003306969 967.277673391869 967.283343543245 967.289013761098 967.294684045428 967.300354396238 967.306024813528 967.3116952973 967.317365847555 967.323036464293 967.328707147517 967.334377897227 967.340048713424 967.345719596109 967.351390545285 980.194309595126 980.200132191883 980.205954857816 980.211777592926 980.217600397214 980.223423270681 980.229246213329 980.235069225159 980.240892306172 980.24671545637 980.252538675753 980.258361964323 980.264185322081 980.270008749028 980.275832245166 980.281655810496 980.287479445018 980.293303148735 980.299126921648 980.304950763757 980.310774675063 980.31659865557 980.322422705276 989.849128120579 989.855065986618 989.861003923897 989.866941932417 989.872880012179 989.878818163186 989.884756385438 989.890694678936 989.896633043682 989.902571479677 989.908509986923 989.91444856542 1024.22504311064 1024.23140056509 1024.23775809846 1024.24411571076 1024.25047340198 1024.25683117213 1024.26318902121 1024.26954694921 1024.27590495616 1024.28226304203 1024.28862120684 1024.29497945058 1024.30133777326 1024.30769617489 1024.31405465545 1024.32041321495 1024.3267718534 1024.3331305708 1024.33948936714 1024.62571693597 1024.63207936544 1024.63844187393 1024.64480446143 1024.65116712796 1024.6575298735 1024.66389269807 1024.67025560165 1024.67661858427 1024.6829816459 1024.68934478657 1024.69570800627 1024.80389483394 1024.8102594764 1024.81662419793 1024.82298899851 1024.82935387816 1024.83571883686 1024.84208387463 1024.84844899146 1024.85481418736 1024.86117946232 1024.86754481635 1024.87391024946 1024.88027576163 1024.88664135288 1037.17792520177 1037.18444447285 1037.19096382588 1037.19748326088 1037.20400277783 1037.21052237674 1037.21704205761 1037.22356182045 1037.23008166525 1037.23660159202 1037.24312160075 1037.24964169146 1037.25616186413 1037.26268211878 1037.2692024554 1037.275722874 1037.28224337457 1037.28876395712 1037.29528462166 1037.30180536817 "
    "1037.30832619667 1037.31484710715 1037.32136809962 1037.32788917408 1038.22205208364 1038.22858448726 1038.2351169731 1038.24164954113 1038.24818219137 1038.25471492382 1038.26124773848 1038.26778063535 1038.27431361443 1038.28084667573 1038.28737981924 1038.29391304497 1038.30044635292 1038.30697974309 1038.31351321548 1038.32004677009 1038.32658040693 1039.22901160479 1039.235556686 1039.24210184966 1039.24864709577 1039.25519242432 1039.26173783531 1039.26828332876 1039.27482890466 1039.28137456301 1039.28792030381 1039.29446612706 1039.30101203278 1039.30755802095 1039.31410409158 1039.32065024467 1039.32719648023 1039.33374279825 1039.34028919873 1039.34683568169 1040.22481095549 1040.23136858588 1040.23792629894 1040.24448409469 1040.25104197312 1040.25759993424 1040.26415797804 1040.27071610453 1040.27727431371 1040.28383260558 1040.29039098014 1040.2969494374 1040.30350797735 1040.3100666 1040.31662530535 1040.32318409339 1040.32974296414 1041.2290907215 1041.2356610201 1041.24223140163 1041.24880186607 1041.25537241343 1041.26194304372 1041.26851375694 1041.27508455308 1041.28165543215 1041.28822639415 1041.29479743909 1041.30136856695 1041.30793977776 1041.3145110715 1041.32108244817 1041.32765390779 1041.33422545035 1041.34079707585 1041.3473687843 1041.35394057569 1041.36051245003 1041.36708440732 1041.37365644756 1041.38022857076 1041.3868007769 1041.39337306601 1041.39994543807 1041.40651789309 1041.41309043107 1041.41966305201 1088.79279195134 1088.79997622919 1088.80716060185 1088.81434506932 1088.82152963161 1088.82871428871 1088.83589904063 1088.84308388737 1088.85026882893 1088.85745386531 1088.86463899652 1098.12539546821 1098.13270343459 1098.14001149825 1098.14731965918 1098.15462791738 1098.16193627285 1098.1692447256 1098.17655327563 1098.18386192294 1098.19117066753 1098.19847950941 1098.20578844857 1098.21309748502 1098.22040661877 1098.2277158498 1098.23502517813 1098.24233460375 1098.24964412667 1098.2569537469 1098.26426346442 "
    "1098.27157327925 1111.23703980607 1111.2445233295 1111.25200695373 1111.25949067876 1111.26697450458 1111.27445843121 1111.28194245863 1111.28942658687 1111.29691081591 1111.30439514576 1111.31187957642 1111.31936410789 1111.32684874018 1111.33433347329 1111.34181830722 1111.34930324196 1112.25572512581 1112.26322237607 1112.27071972739 1112.27821717979 1112.28571473327 1112.29321238783 1112.30071014346 1112.30820800018 1112.31570595798 1112.32320401687 1112.33070217685 1112.33820043792 1112.34569880008 1112.41318860905 1112.42068798226 1112.42818745657 1112.43568703201 1112.44318670856 1112.45068648624 1112.45818636504 1112.46568634496 1112.47318642601 1112.48068660819 1112.48818689151 1112.49568727595 1112.50318776154 1112.57819818028 1112.58569977849 1112.59320147786 1112.60070327839 1112.60820518009 1112.61570718295 1112.62320928699 1112.63071149219 1112.63821379856 1112.64571620611 1112.65321871484 1112.66072132475 1114.26108654592 1114.26861085524 1114.27613526618 1114.28365977874 1114.29118439293 1114.29870910874 1114.30623392618 1114.31375884525 1114.32128386596 1114.32880898829 1114.33633421227 1115.25517475482 1115.2627124958 1115.27025033867 1115.27778828344 1115.28532633011 1115.29286447867 1115.30040272913 1115.3079410815 1115.31547953577 1115.32301809195 1115.33055675004 1115.33809551004 1115.34563437195 1130.2815779083 1130.28932013803 1130.29706247383 1130.30480491569 1130.31254746363 1130.32029011764 1130.32803287773 1130.33577574389 1130.34351871613 1130.35126179446 1130.35900497886 1147.66276619636 1147.67074837394 1147.67873066254 1147.68671306219 1147.69469557288 1147.70267819461 1147.71066092738 1147.7186437712 1147.72662672607 1147.73460979199 1147.74259296897 1147.750576257 1147.75855965609 1147.76654316625 1147.77452678746 1182.98466783959 1182.99314891893 1183.00163011987 1183.01011144242 1183.01859288659 1183.02707445237 1183.03555613976 1183.04403794878 1183.05251987942 1183.06100193168 1183.06948410557 1183.07796640108 1183.1712796801 "
    "1183.17976343539 1183.18824731234 1183.19673131095 1183.20521543123 1183.21369967318 1183.22218403681 1183.23066852212 1183.2391531291 1183.24763785777 1183.25612270812 1183.26460768015 1183.27309277388 1183.68052043171 1183.68901149146 1183.69750267303 1183.70599397643 1183.71448540165 1183.7229769487 1183.73146861758 1183.7399604083 1183.74845232085 1183.75694435525 1183.76543651148 1183.77392878956 1188.27493289966 1188.28349000304 1188.29204722968 1188.30060457956 1188.30916205269 1188.31771964907 1188.32627736871 1188.3348352116 1188.34339317776 1188.35195126718 1188.36050947987 1209.74923184622 1209.75810103042 1209.76697034466 1209.77583978896 1209.78470936331 1209.79357906772 1209.80244890218 1209.81131886671 1209.82018896131 1209.82905918597 1209.83792954071 1209.87341226042 1209.88228326555 1209.89115440077 1209.90002566609 1209.9088970615 1209.917768587 1209.92664024261 1209.93551202832 1209.94438394413 1209.95325599005 1209.96212816609 1214.99579864251 1215.00474492341 1215.01369133607 1215.02263788047 1215.03158455663 1215.04053136455 1215.04947830422 1215.05842537566 1215.06737257887 1215.07631991384 1215.08526738058 1215.0942149791 1215.10316270939 1215.11211057146 1240.4233010027 1240.43262565993 1240.44195045736 1240.45127539499 1240.46060047282 1240.46992569085 1240.47925104909 1240.48857654754 1240.4979021862 1240.50722796507 1240.51655388417 1240.52587994349 1240.53520614303 1240.5445324828 1243.04905650569 1243.05842068214 1243.06778499968 1243.0771494583 1243.08651405802 1243.09587879883 1243.10524368075 1243.11460870376 1243.12397386788 1243.13333917311 1243.14270461945 1243.15207020691 1243.16143593548 1243.17080180517 1243.18016781599 1243.18953396794 1250.94602927114 1250.95551280543 1250.96499648351 1250.97448030538 1250.98396427106 1250.99344838053 1251.00293263381 1251.0124170309 1251.0219015718 1251.03138625652 1251.04087108505 1251.05035605741 1261.31128248752 1261.32092383336 1261.33056532659 1261.34020696722 1261.34984875525 "
    "1261.35949069069 1261.36913277353 1261.37877500379 1261.38841738147 1261.39805990657 1261.40770257909 1261.94792763215 1261.95757871339 1261.96722994224 1261.97688131873 1261.98653284283 1261.99618451457 1262.00583633394 1262.01548830095 1262.02514041559 1262.03479267788 1262.04444508782 1262.05409764541 1262.06375035065 1262.07340320354 1262.32442921749 1262.33408605841 1262.34374304707 1262.35340018349 1262.36305746766 1262.3727148996 1262.3823724793 1262.39203020678 1262.40168808202 1262.41134610504 1262.42100427584 1262.43066259442 1262.44032106079 1263.28117374346 1263.29084522825 1263.30051686113 1263.31018864211 1263.31986057118 1263.32953264835 1263.33920487361 1263.34887724699 1263.35854976847 1263.36822243806 1263.37789525577 1263.3875682216 1263.39724133555 1263.40691459763 1265.31544052296 1265.32514318102 1265.33484598788 1265.34454894355 1265.35425204803 1265.36395530132 1265.37365870343 1265.38336225437 1265.39306595413 1265.40276980271 1265.41247380013 1265.42217794639 1273.46930606141 1273.47913417338 1273.48896243705 1273.49879085242 1273.5086194195 1273.51844813829 1273.5282770088 1273.53810603102 1273.54793520496 1273.55776453063 1273.56759400803 1273.57742363715 1273.58725341802 1273.59708335062 1273.60691343496 1273.61674367105 1273.62657405888 1281.32087896611 1281.33082864257 1281.34077847355 1281.35072845906 1281.3606785991 1281.37062889368 1281.38057934279 1281.39052994644 1281.40048070464 1281.41043161738 1281.42038268468 1281.43033390653 1281.44028528294 1281.45023681392 1281.46018849946 1281.47014033957 1281.48009233426 1293.16997731079 1293.18011185941 1293.19024656688 1293.2003814332 1293.21051645839 1293.22065164243 1293.23078698534 1293.24092248711 1293.25105814776 1293.26119396729 1293.27132994569 1293.28146608298 1409.30396671911 1409.31600329167 1409.32804006984 1409.34007705362 1409.35211424301 1409.36415163802 1409.37618923866 1409.38822704493 1409.40026505684 1409.4123032744 1409.4243416976 1409.43638032645 1439.81291838247 "
    "1439.82548173949 1439.83804531576 1439.85060911129 1439.86317312607 1439.87573736013 1439.88830181346 1439.90086648606 1439.91343137795 1439.92599648913 1439.93856181961 1439.97625912687 1439.98882533458 1440.00139176163 1440.013958408 1440.0265252737 1440.03909235875 1440.05165966315 1440.0642271869 1440.07679493 1440.08936289247 1440.10193107432 1456.39679555874 1456.40964999523 1456.42250465864 1456.43535954896 1456.44821466621 1456.46107001039 1456.47392558151 1456.48678137957 1456.49963740459 1456.51249365655 1456.52535013548 1456.53820684138 1456.55106377425 1456.5639209341 1456.57677832093 1469.49643693345 1469.5095236508 1469.52261060125 1469.5356977848 1469.54878520145 1469.56187285121 1469.57496073409 1469.58804885009 1469.60113719922 1469.61422578149 1469.6273145969 1469.64040364545 1477.46994793765 1477.48317705841 1477.49640641608 1477.50963601066 1477.52286584217 1477.53609591059 1477.54932621596 1477.56255675826 1477.5757875375 1477.58901855369 1477.60224980685 1477.61548129696 1477.62871302405 1477.64194498811 1477.65517718916 1477.6684096272 1477.68164230223 1477.69487521426 1477.7081083633 1477.72134174936 1478.91331787051 1478.9265728516 1478.93982807029 1478.9530835266 1478.96633922051 1478.97959515204 1478.99285132121 1479.006107728 1479.01936437244 1479.03262125452 1479.04587837426 1480.17360285036 1480.18688043216 1480.20015825217 1480.2134363104 1480.22671460685 1480.23999314153 1480.25327191444 1480.2665509256 1480.279830175 1480.29310966266 1480.30638938858 1480.31966935276 1480.33294955522 1480.34622999596 1480.35951067499 1480.37279159231 1480.38607274793 1480.39935414185 1480.41263577409 1480.42591764464 1480.73146645809 1480.74475405022 1480.75804188082 1480.77132994991 1480.78461825749 1480.79790680357 1480.81119558816 1480.82448461126 1480.83777387287 1480.85106337301 1480.86435311168 1480.87764308888 1480.89093330463 1480.90422375893 1480.91751445178 1480.9308053832 1480.94409655318 1480.95738796174 1480.97067960888 1480.983971494 "
    "1480.99726361894 1481.01055598187 1481.0238485834 1481.03714142355 1481.05043450231 1481.06372781971 1481.07702137574 1481.34294261915 1481.35624118796 1481.36953999554 1481.38283904191 1481.39613832706 1481.409437851 1481.42273761375 1481.4360376153 1481.44933785567 1481.46263833486 1481.47593905287 1481.48924000971 1481.50254120539 1481.51584263992 1481.5291443133 1481.54244622554 1481.55574837664 1481.56905076662 1481.58235339547 1481.5956562632 1481.60895936983 1481.62226271535 1481.63556629978 1481.64887012311 1481.66217418536 1481.67547848654 1482.36763154258 1482.38094851586 1482.39426572841 1482.40758318025 1482.42090087136 1482.43421880177 1482.44753697147 1482.46085538047 1482.47417402879 1482.48749291642 1482.50081204337 1482.51413140965 1482.52745101526 1482.54077086022 1493.62532276273 1493.63884277381 1493.65236302966 1493.66588353027 1493.67940427566 1493.69292526583 1493.70644650079 1493.71996798055 1493.73348970511 1493.74701167448 1493.76053388867 1493.77405634768 1511.59045068974 1511.60429789207 1511.6181453481 1511.63199305785 1511.64584102131 1511.65968923849 1511.6735377094 1511.68738643405 1511.70123541244 1511.71508464459 1511.72893413049 1599.95476419777 1599.97027768381 1599.98579147069 1600.00130555844";

    const char* RawY =
    "0 0 0 0 0 0 0 0 0 193.855026245117 641.106506347656 877.26220703125 700.196716308594 274.883911132813 0 0 0 0 0 0 0 0 0 0 224.89469909668 511.670349121094 595.136657714844 452.148010253906 234.596389770508 74.6820220947266 0 0 0 0 0 0 0 0 119.225784301758 185.543075561523 200.134750366211 188.900192260742 160.073196411133 0 0 0 0 0 0 0 172.296859741211 200.144027709961 165.512985229492 0 0 0 0 0 0 0 0 83.4415893554688 208.525405883789 177.816024780273 100.466018676758 0 0 0 0 0 0 0 0 93.3426513671875 177.81330871582 221.383926391602 233.153823852539 185.899673461914 84.1839904785156 0 0 0 0 0 0 0 0 126.048049926758 184.371139526367 97.8295288085938 0 0 0 0 0 0 0 0 25.8681793212891 261.258850097656 531.637878417969 574.53076171875 413.904724121094 218.982650756836 112.732650756836 0 0 0 0 0 0 0 0 94.0497131347656 191.148391723633 161.991897583008 0 0 0 0 0 0 0 402.995788574219 1600.27600097656 3017.67749023438 3685.970703125 3078.0078125 1699.85888671875 531.254028320313 44.9793395996094 0 0 0 0 0 0 0 0 3.05120849609375 326.982971191406 918.194396972656 1295.37927246094 1141.49755859375 577.818969726563 26.3272705078125 0 0 0 0 0 0 0 0 125.907791137695 264.531921386719 266.280029296875 158.506088256836 0 0 0 0 0 0 0 0 45.1534271240234 245.012252807617 337.062133789063 230.558639526367 35.3934326171875 0 0 0 0 0 0 0 0 21.4011077880859 269.609924316406 302.905334472656 3843.85766601563 9906.61328125 14627.65625 14086.5751953125 8667.4873046875 2666.59497070313 152.549850463867 452.571411132813 21.2885437011719 0 0 0 0 0 0 0 0 0 242.630386352539 1476.34033203125 2982.52856445313 3656.2958984375 2926.99194335938 1440.77648925781 378.651611328125 617.814758300781 1036.13317871094 1038.09765625 677.166748046875 273.266662597656 106.01123046875 0 0 0 0 0 0 0 0 0 644.765441894531 1669.39282226563 2428.14697265625 2279.11108398438 1350.7001953125 456.782958984375 195.992416381836 144.774459838867 138.42707824707 203.370742797852 302.340209960938 350.181884765625 "
    "314.466613769531 225.259658813477 125.442489624023 0 0 0 0 0 0 0 0 0 229.676986694336 1286.525390625 3076.466796875 4448.58984375 4288.94775390625 2695.29956054688 856.980102539063 0 0 0 0 0 0 0 0 0 0 297.09423828125 790.947937011719 1078.3701171875 895.997009277344 377.297119140625 0 0 0 0 0 0 0 0 0 54.2761840820313 380.800170898438 604.678894042969 530.256896972656 239.020004272461 14.5890960693359 0 0 0 0 0 0 0 0 0 315.951721191406 1584.78002929688 3584.27490234375 4964.8896484375 4550.10595703125 2648.34790039063 761.737854003906 336.893249511719 327.000427246094 103.404342651367 0 0 0 0 0 0 0 0 137.308151245117 232.669479370117 225.336959838867 131.02082824707 0 0 0 0 0 0 0 0 0 309.901550292969 806.588073730469 1159.07531738281 1105.30993652344 695.416931152344 245.27473449707 13.4951171875 0 0 0 0 0 0 0 0 114.433959960938 215.006851196289 272.628845214844 187.665817260742 12.0498352050781 0 0 0 0 0 0 0 0 127.550247192383 182.018173217773 211.423233032227 191.385147094727 141.782333374023 0 0 0 0 0 0 0 0 0 745.767456054688 2422.9541015625 3720.92529296875 3568.47998046875 2088.86743164063 510.764953613281 183.886978149414 136.265151977539 0 0 0 0 0 0 0 0 171.257888793945 188.640060424805 192.395614624023 154.632247924805 0 0 0 0 0 0 0 0 130.887710571289 642.11865234375 1160.51184082031 1294.43505859375 977.964050292969 544.39794921875 308.192199707031 211.437362670898 76.1909332275391 0 0 0 0 0 0 0 0 0 229.855178833008 543.525207519531 586.220275878906 314.952575683594 0 0 0 0 0 0 0 0 0 154.422073364258 196.758010864258 149.831283569336 0 0 0 0 0 0 0 0 123.318832397461 188.429489135742 169.386795043945 0 0 0 0 0 0 0 0 2959.08642578125 8616.0419921875 13691.96484375 14140.76953125 9577.6708984375 3752.03759765625 393.419616699219 324.138061523438 232.52278137207 0 0 0 0 0 0 0 0 0 133.11164855957 205.964584350586 199.730422973633 139.437789916992 0 0 0 0 0 0 0 0 101.496200561523 185.133193969727 259.655029296875 244.710922241211 160.866897583 0 0 0 0 0 0 0 0 "
    "171.158157348633 209.737930297852 216.904006958008 153.206130981445 0 0 0 0 0 0 0 0 0 1064.01928710938 2744.22631835938 3925.62744140625 3728.50854492188 2484.57104492188 1484.09924316406 1184.474609375 801.294799804688 276.304870605469 0 0 0 0 0 0 0 0 0 0 300.0966796875 1263.98876953125 2300.80615234375 2527.240234375 1658.1162109375 416.934509277344 362.052856445313 510.349609375 422.890075683594 293.749877929688 96.1615142822266 0 0 0 0 0 0 0 0 277.112182617188 552.278137207031 634.911743164063 465.758728027344 230.118118286133 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 338.728454589844 100.444885253906 1113.40734863281 4002.13525390625 6734.62841796875 7179.69189453125 4943.12939453125 1888.85778808594 87.8339996337891 0 0 0 0 0 0 0 0 166.607498168945 203.203231811523 88.5599517822266 0 0 134.797103881836 252.799118041992 321.236206054688 317.407653808594 177.626174926758 0 0 0 0 0 0 0 0 420.235168457031 1400.63110351563 2127.62890625 2036.72399902344 1375.9619140625 1018.35345458984 909.901550292969 528.322021484375 118.658309936523 0 0 0 0 0 0 0 0 6.52516174316406 184.584854125977 399.570495605469 472.823852539063 311.700012207031 67.0648040771484 0 0 0 0 0 0 0 0 4.97833251953125 518.760131835938 1195.45788574219 1522.00109863281 1192.40087890625 457.996826171875 236.555740356445 455.735107421875 494.134094238281 419.947204589844 169.757431030273 0 0 0 0 0 0 0 0 158.202651977539 249.356582641602 255.182876586914 181.06364440918 0 0 0 0 0 0 0 0 173.365737915039 243.861038208008 222.755081176758 104.503341674805 0 0 0 0 0 0 0 0 127.732864379883 184.066452026367 159.648910522461 0 0 0 0 0 0 0 0 0 216.264389038086 564.540100097656 758.490661621094 593.172241210938 186.406509399414 0 0 0 0 0 0 0 0 0 57.8870544433594 189.093154907227 180.032363891602 0 0 0 0 0 0 0 0 107.181259155273 588.624389648438 3296.2294921875 6859.40966796875 8629.48046875 6954.76904296875 3204.29370117188 530.406066894531 701.101257324219 446.318359375 172.379989624023 0 0 0 0 0 0 0 0 "
    "106.025817871094 219.603103637695 171.653244018555 0 0 0 0 0 0 0 0 66.6060028076172 816.289489746094 2058.00170898438 2839.88916015625 2264.32885742188 887.272766113281 1098.07897949219 1184.8994140625 515.840759277344 0 0 0 0 0 0 0 0 0 107.923675537109 213.511978149414 238.096145629883 157.005569458008 0 0 0 0 0 0 0 0 84.7611541748047 279.91943359375 997.471740722656 1668.52087402344 1678.548828125 1062.6884765625 456.879150390625 261.066772460938 414.542724609375 400.814636230469 181.47590637207 0 0 0 0 0 0 0 0 162.703384399414 402.678283691406 606.938415527344 584.632751464844 322.495727539063 0 0 0 0 0 0 0 0 203.609512329102 221.824447631836 103.749816894531 0 0 0 0 0 0 0 0 139.273239135742 246.041458129883 237.826309204102 138.225112915039 0 0 0 102.917053222656 540.358703613281 1507.14807128906 2255.67041015625 2121.10278320313 1207.01428222656 254.796005249023 0 0 0 0 0 0 0 0 0 180.893783569336 216.125747680664 186.408401489258 92.5867767333984 0 0 0 0 0 0 0 0 67.1141357421875 314.660339355469 430.744506835938 249.505142211914 110.873168945313 311.952087402344 298.573059082031 158.444198608398 0 0 0 0 0 0 0 0 0 203.841903686523 330.667297363281 305.950500488281 171.00895690918 0 0 0 0 0 0 0 0 150.461807250977 187.074295043945 122.066360473633 0 0 0 0 0 0 0 0 128.328475952148 228.142013549805 224.649185180664 115.597442626953 0 0 0 0 0 0 0 0 121.621292114258 209.864395141602 180.903396606445 0 0 0 0 0 0 0 0 180.190689086914 221.191635131836 230.223587036133 203.161819458008 163.631942749023 0 0 0 103.920654296875 254.635330200195 239.919662475586 117.75276184082 0 0 0 0 0 0 0 0 127.274429321289 277.754516601563 418.050842285156 364.745910644531 108.102828979492 0 0 0 0 0 0 0 0 122.235366821289 204.649490356445 1283.6416015625 2732.66333007813 3403.52026367188 2677.83666992188 1189.97729492188 91.4230346679688 0 0 0 0 0 0 0 0 179.184860229492 218.383316040039 188.207290649414 103.431289672852 0 0 0 0 0 19.3411254882813 320.543273925781 708.724914550781 "
    "746.338012695313 324.464965820313 498.983642578125 805.302062988281 584.451416015625 150.821365356445 0 0 0 0 0 0 0 0 18.6659393310547 273.791931152344 556.722961425781 580.784423828125 324.137634277344 118.86296081543 192.937911987305 133.19303894043 0 0 0 0 0 0 0 0 156.306106567383 528.205383300781 857.985168457031 865.72802734375 528.765258789063 126.998458862305 0 0 0 0 0 0 0 0 141.282363891602 213.537307739258 188.727676391602 83.9633941650391 0 0 0 0 0 0 0 0 53.5650329589844 300.0869140625 496.208679199219 543.627502441406 469.730163574219 370.066101074219 289.648376464844 183.115615844727 57.3272399902344 0 0 0 0 0 0 0 0 128.359512329102 282.553894042969 428.125305175781 518.781311035156 625.49609375 722.9619140625 704.251159667969 596.822631835938 501.902587890625 382.079040527344 159.27229309082 0 0 0 0 0 0 0 0 65.7273559570313 820.538696289063 2162.48120117188 3311.76513671875 3299.95263671875 2090.16650390625 672.241455078125 0 0 0 0 0 0 0 0 0 0 308.417297363281 836.372497558594 1054.75146484375 828.995056152344 520.746643066406 281.113220214844 0 0 0 0 0 0 0 0 0 135.923385620117 224.629806518555 176.55793762207 0 0 0 0 0 0 0 0 0 243.977737426758 679.087585449219 930.161682128906 790.303100585938 379.179931640625 152.314804077148 0 0 0 0 0 0 0 0 136.929214477539 317.641784667969 361.6630859375 256.365356445313 53.1949462890625 0 0 0 0 0 0 0 0 79.8312377929688 199.234237670898 225.433242797852 110.191116333008 0 0 0 0 0 0 0 0 88.5470733642578 328.809631347656 486.254577636719 566.095397949219 679.935241699219 807.850219726563 851.315795898438 785.165161132813 654.703979492188 527.893920898438 423.899841308594 256.051086425781 0 0 0 0 0 0 0 0 0 0 325.090454101563 1122.31469726563 1884.14013671875 2005.18811035156 1370.16577148438 511.172485351563 0 0 0 0 0 0 0 0 0 144.137466430664 222.952560424805 139.711410522461 0 0 0 31.4209136962891 456.088439941406 906.362609863281 992.082092285156 640.056396484375 191.88249206543 0 0 0 0 0 0 0 0 0 142.481643676758 "
    "248.23698425293 361.091796875 321.38818359375 84.5967559814453 0 0 0 0 0 0 0 0 68.5481414794922 229.667556762695 342.556030273438 233.595016479492 12.875 284.913818359375 586.492919921875 765.540771484375 846.076843261719 837.908752441406 736.949829101563 570.628051757813 422.32958984375 338.351196289063 265.928649902344 102.936645507813 0 0 0 0 0 0 0 0 111.217437744141 194.165725708008 498.359680175781 810.423156738281 786.201354980469 424.384338378906 32.8225250244141 0 0 0 0 0 0 0 0 126.047775268555 188.677780151367 216.424392700195 207.760787963867 193.143905639648 173.67204284668 0 0 0 0 0 0 0 0 230.711318969727 263.243713378906 218.297134399414 121.326461791992 0 0 0 0 0 0 0 0 85.8698425292969 222.64518737793 225.071212768555 111.491928100586 0 0 0 0 0 0 0 0 158.272689819336 319.939025878906 405.672607421875 543.889831542969 709.350402832031 762.2890625 669.657836914063 464.710327148438 230.338363647461 135.231704711914 0 0 0 0 0 0 0 0 112.85188293457 340.410949707031 556.054382324219 649.1904296875 642.006286621094 595.709777832031 552.84423828125 532.100952148438 509.958984375 393.80126953125 147.021102905273 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 163.031021118164 257.087280273438 222.833755493164 69.1905975341797 0 0 0 0 0 0 0 0 0 193.01008605957 1283.4189453125 2651.75854492188 3263.72827148438 2533.265625 1061.58337402344 237.298934936523 292.41845703125 36.6092681884766 0 0 0 0 0 0 0 0 0 412.251708984375 1201.08520507813 1691.83618164063 1483.95202636719 787.549011230469 140.731460571289 0 0 0 0 0 0 0 0 155.059463500977 197.582107543945 226.128463745117 389.715087890625 812.975402832031 1328.24084472656 1494.00390625 1091.52282714844 416.5791015625 13.3121490478516 0 0 0 0 0 0 0 0 30.8641204833984 259.902893066406 398.201293945313 307.794982910156 89.9954833984375 0 0 0 0 0 0 0 0 250.239639282227 294.914489746094 240.247360229492 36.3681182861328 0 0 0 0 0 0 0 0 168.141342163086 211.644943237305 185.341293334961 0 0 0 0 0 0 0 0 0 1255.41430664063 "
    "4270.45751953125 7186.3505859375 7656.57861328125 5270.84912109375 2048.24633789063 131.94660949707 0 0 0 0 0 0 0 0 171.324295043945 234.151138305664 188.494430541992 85.8521118164063 0 0 0 0 0 0 0 177.790451049805 196.738876342773 191.143508911133 173.924301147461 0 0 0 0 0 0 0 0 27.4716339111328 870.664428710938 2696.56469726563 4313.33056640625 4373.56005859375 2848.65014648438 1105.826171875 363.445678710938 95.8519592285156 0 0 0 0 0 0 0 0 165.054183959961 190.630477905273 212.371444702148 119.549850463867 0 0 0 0 144.282302856445 1497.27661132813 3014.11791992188 3372.33959960938 2141.125 440.955444335938 486.9052734375 473.754028320313 237.800888061523 10.4761962890625 0 0 0 0 0 0 0 0 187.649795532227 717.752685546875 1004.04693603516 895.356262207031 611.759887695313 231.517959594727 0 0 0 0 0 0 0 0 0 0 380.191772460938 1166.65649414063 1580.28991699219 1321.69921875 695.5244140625 219.708572387695 60.3770294189453 0 0 0 0 0 0 0 0 168.744674682617 496.461608886719 797.966979980469 819.004150390625 524.868408203125 148.768051147461 0 0 0 0 0 0 0 0 114.591278076172 230.604568481445 404.740417480469 400.008056640625 149.154037475586 0 0 0 0 0 0 0 0 102.017929077148 332.576171875 431.327087402344 395.180480957031 291.81103515625 179.464797973633 0 0 0 0 0 0 0 0 328.818298339844 432.372192382813 513.603515625 524.003845214844 455.190307617188 323.07763671875 0 0 0 0 0 0 0 0 0 500.822875976563 1250.17626953125 1691.61279296875 1446.947265625 741.710021972656 242.135177612305 131.856307983398 0 0 0 0 0 0 0 0 106.934600830078 416.481811523438 591.3037109375 471.31884765625 154.692947387695 0 0 0 0 0 0 0 0 58.2242736816406 451.217834472656 867.215209960938 952.283020019531 683.782348632813 327.990234375 53.9348297119141 0 0 0 0 0 0 0 0 112.282455444336 194.305465698242 247.864517211914 283.480651855469 251.062362670898 154.861434936523 0 0 0 0 0 0 0 0 0 2550.76782226563 7417.4248046875 11472.3154296875 11331.73046875 7126.333984375 2372.173828125 154.968887329 0 0 "
    "0 0 0 0 0 0 171.941879272461 235.122146606445 189.759658813477 86.6224975585938 0 0 0 0 0 0 0 0 161.515548706055 191.835342407227 263.086181640625 280.552551269531 146.46711730957 354.734191894531 1819.36071777344 4197.60791015625 5904.71484375 5515.97998046875 3378.09521484375 1216.07165527344 240.816360473633 160.06672668457 0 0 0 0 0 0 0 0 168.925979614258 417.149658203125 1732.84704589844 3627.1572265625 4610.03076171875 3790.7890625 1980.00671386719 757.338623046875 314.47314453125 0 0 0 0 0 0 0 0 0 151.324813842773 253.94270324707 329.051940917969 266.352722167969 111.870147705078 0 196.40153503418 1169.40258789063 2201.71215820313 2499.97778320313 1842.07995605469 847.819702148438 294.143676757813 154.02375793457 0 0 0 0 0 0 0 0 0 263.716003417969 663.424133300781 945.621276855469 900.181091308594 586.183349609375 242.737838745117 0 0 0 0 0 0 0 0 0 87.7988586425781 273.416259765625 275.446044921875 122.881698608398 0 0 0 0 0 0 0 0 139.006607055664 632.857299804688 1046.19299316406 1076.18725585938 721.538818359375 282.741760253906 33.4364013671875 0 92.9069519042969 201.903335571289 216.809951782227 66.0712280273438 0 0 0 0 0 0 0 0 0 262.810607910156 583.060974121094 697.79833984375 522.555419921875 181.537673950195 0 0 0 0 0 0 0 0 258.55615234375 289.532775878906 243.079086303711 173.991165161133 0 0 0 0 0 0 0 0 37.7136077880859 214.55876159668 243.370010375977 18.9360961914063 35.8999176025391 191.428115844727 166.77082824707 0 0 0 0 0 0 0 0 0 258.869323730469 651.99658203125 911.448181152344 810.898193359375 421.798156738281 54.3055572509766 0 0 0 0 0 0 0 0 60.1159057617188 292.62109375 475.166442871094 465.994323730469 265.969482421875 59.4075927734375 0 0 0 0 0 0 0 0 129.671829223633 203.134628295898 214.902053833008 142.065078735352 0 0 0 0 0 0 0 0 68.9898071289063 258.451477050781 349.82421875 288.4658203125 184.60481262207 0 0 0 0 0 0 0 0 60.4415130615234 757.99267578125 4146.55859375 8593.078125 10882.390625 9079.6171875 4740.5390625 1125.72338867 " 
    "0 0 0 0 0 0 0 0 0 102.086837768555 209.562088012695 285.004333496094 167.125106811523 0 0 0 0 0 0 0 0 153.054061889648 218.63932800293 227.450668334961 199.439254760742 191.222061157227 179.493179321289 0 0 0 0 0 0 0 0 136.84049987793 243.085250854492 175.279190063477 0 0 0 165.884048461914 1627.97961425781 4570.39892578125 7233.904296875 7538.84521484375 5242.4697265625 2183.01293945313 247.249221801758 94.0259094238281 0 0 0 0 0 0 0 0 0 708.975402832031 2684.54736328125 4892.7626953125 5664.70263671875 4346.48974609375 2153.0361328125 792.776916503906 328.806335449219 26.2600402832031 0 0 0 0 0 0 0 0 0 600.651245117188 1863.01184082031 2856.58935546875 2825.29223632813 1829.23559570313 678.683837890625 156.775985717773 0 0 0 0 0 0 0 0 177.269912719727 213.871047973633 146.206100463867 0 0 0 0 0 0 0 0 0 438.628295898438 1067.17602539063 1423.88037109375 1322.46948242188 846.637451171875 222.935501098633 8.62030029296875 0 0 0 0 0 0 0 0 1.40678405761719 204.166915893555 495.588256835938 711.029602050781 672.086486816406 440.136291503906 197.460708618164 25.0921783447266 0 0 0 0 0 0 0 0 181.639419555664 454.123840332031 630.59228515625 540.996643066406 248.280014038086 0 0 0 0 0 0 0 0 0 0 272.537475585938 507.36669921875 482.3701171875 228.514022827148 0 0 0 0 0 0 0 0 0 173.016799926758 282.362365722656 239.30632019043 89.6678161621094 0 0 0 0 0 0 0 0 163.693313598633 238.812973022461 181.945022583008 0 0 0 0 0 0 0 0 154.68586730957 251.716812133789 301.325927734375 285.520263671875 212.902420043945 125.696426391602 0 0 0 0 0 0 0 0 144.465744018555 280.485534667969 282.486389160156 171.469833374023 0 0 0 0 0 0 0 0 168.80158996582 1852.96484375 4451.087890625 6223.240234375 5698.3251953125 3317.40869140625 947.944641113281 0 72.4445648193359 222.517288208008 190.692306518555 8.54414367675781 0 0 0 0 0 0 0 0 173.518569946289 287.66455078125 315.665649414063 180.375747680664 0 0 0 0 0 0 0 0 22.9653015136719 377.313659667969 1943.11608886719 3810.626464843 4614.140625 "
    "3648.7470703125 1708.49145507813 216.385025024414 180.797805786133 220.440505981445 66.8912200927734 0 0 0 0 0 143.90119934082 216.902420043945 185.753768920898 98.3815460205078 0 0 0 0 0 0 0 0 162.759536743164 215.904861450195 208.337875366211 127.708084106445 0 0 0 0 0 0 0 0 105.314514160156 217.56315612793 204.984756469727 103.096450805664 1431.53564453125 2977.5546875 3651.64916992188 2931.84326171875 1398.70080566406 87.1452484130859 320.6357421875 384.557983398438 269.201110839844 237.787063598633 238.22102355957 131.059158325195 0 0 0 0 0 0 0 0 82.7467803955078 540.114013671875 1467.232421875 2395.43090820313 2641.54028320313 2010.16149902344 989.484558105469 192.628158569336 0 0 179.865524291992 275.410278320313 175.700149536133 0 0 0 0 0 0 0 0 59.9419708251953 485.762634277344 988.358093261719 1207.15466308594 963.307312011719 438.903381347656 31.6091766357422 0 0 0 0 0 47.9591827392578 196.635208129883 252.011154174805 211.016983032227 150.474960327148 0 0 0 0 0 0 0 0 168.648544311523 212.75212097168 246.239303588867 241.005111694336 180.050369262695 0 0 0 0 93.9449157714844 308.76171875 510.256713867188 547.370849609375 379.794067382813 126.207992553711 0 0 0 0 0 0 0 0 165.212295532227 203.172348022461 187.95915222168 180.575973510742 0 0 0 0 0 0 0 0 75.0355987548828 196.667404174805 286.693542480469 310.418090820313 283.288269042969 260.312194824219 292.165649414063 351.78125 356.774780273438 261.874694824219 112.601119995117 0 0 0 0 0 0 0 0 147.65412902832 199.247695922852 206.355667114258 137.159103393555 0 0 0 0 0 0 0 0 153.178085327148 250.377090454102 305.382019042969 294.765869140625 215.430770874023 81.4587097167969 0 0 0 0 0 0 0 0 89.8516693115234 217.64045715332 309.367736816406 283.979248046875 149.045394897461 0 0 0 182.38005065918 418.089721679688 1096.13757324219 1732.892578125 1712.91027832031 1019.67010498047 253.890274047852 63.1096954345703 0 0 0 0 0 0 0 0 146.927871704102 197.823593139648 450.705383300781 1247.59985351563 "
    "2147.16479492188 2419.29248046875 1796.34436035156 783.829040527344 106.181396484375 0 0 0 0 0 0 0 0 0 334.013916015625 997.858215332031 1538.47912597656 1550.04187011719 1073.02502441406 532.429992675781 248.089309692383 200.781692504883 213.394241333008 177.545181274414 0 0 0 0 0 0 0 0 26.6942443847656 209.29020690918 520.969909667969 869.404174804688 1070.1162109375 975.004333496094 655.297485351563 316.672058105469 99.7084808349609 0 0 0 0 0 0 0 0 79.5495910644531 288.576049804688 554.977966308594 746.154174804688 740.367309570313 521.067321777344 227.974136352539 72.0663757324219 0 0 0 0 0 0 0 0 171.190017700195 314.421569824219 429.333374023438 416.543640136719 251.908309936523 31.249267578125 0 0 0 0 0 0 0 0 180.657669067383 192.169876098633 162.963485717773 0 0 0 0 0 0 0 0 155.606674194336 333.596923828125 371.658142089844 244.003341674805 111.538848876953 0 0 0 55.3578186035156 199.379867553711 277.664733886719 246.347457885742 148.267105102539 0 0 0 0 0 0 0 0 90.9604797363281 198.196731567383 372.608276367188 566.121337890625 624.478515625 473.921447753906 201.671371459961 0 0 0 0 0 0 0 0 0 120.198684692383 400.147033691406 571.140441894531 478.692626953125 229.758438110352 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 152.600845336914 225.899429321289 240.899063110352 190.175918579102 0 0 0 0 0 0 0 0 225.349227905273 261.482971191406 145.197463989258 0 0 0 0 0 0 0 0 64.5845184326172 213.417953491211 267.813903808594 201.623031616211 117.913467407227 0 0 0 0 0 0 0 0 182.848587036133 203.188278198242 155.740859985352 0 0 0 0 0 0 0 0 184.153060913086 247.411849975586 282.429565429688 316.498046875 347.890441894531 314.713134765625 179.080673217773 0 0 0 0 0 0 0 0 115.904327392578 226.538375854492 215.979263305664 129.574813842773 0 0 0 0 0 0 0 0 129.243606567383 308.855651855469 363.072937011719 252.200698852539 36.5062561035156 0 0 0 0 0 0 0 0 168.485580444336 221.236099243164 203.256454467773 118.937698364258 0 0 0 0 0 0 0 0 135.065872192383 197.25260925293 "
    "164.043045043945 0 0 0 0 0 0 0 0 101.22346496582 199.961135864258 181.580459594727 0 0 0 0 0 0 0 0 171.627029418945 216.893325805664 165.188735961914 0 0 0 0 0 0 0 0 184.577438354492 315.924865722656 400.736938476563 401.748779296875 309.456359863281 177.526077270508 0 0 0 0 0 0 0 0 71.3968505859375 196.379440307617 317.622741699219 323.017578125 218.84977722168 108.977661132813 0 0 0 0 0 0 0 0 143.710311889648 248.819686889648 318.435913085938 338.807861328125 306.490661621094 239.977798461914 197.418716430664 128.426620483398 0 0 0 0 0 0 0 0 126.655654907227 226.479202270508 229.53288269043 179.628677368164 0 0 0 0 0 0 0 0 144.30647277832 214.861099243164 189.256454467773 0 0 0 0 0 0 0 0 188.085342407227 308.395935058594 374.08056640625 337.305053710938 193.459030151367 33.2561950683594 0 0 0 0 0 0 0 0 101.20735168457 200.764785766602 247.183944702148 206.08268737793 87.9416961669922 0 0 0 0 0 0 0 0 95.3595581054688 225.91584777832 248.077072143555 250.59260559082 257.277648925781 180.943740844727 0 0 0 0 0 0 0 0 155.655776977539 238.868362426758 250.283981323242 157.468978881836 0 0 0 0 0 0 0 0 165.849624633789 341.285217285156 449.087829589844 465.197448730469 414.669006347656 350.80908203125 287.216369628906 202.29280090332 102.295700073242 0 0 0 0 0 0 0 0 122.961654663086 216.524826049805 227.398483276367 197.743057250977 252.932815551758 368.044921875 412.497924804688 332.376770019531 189.94221496582 0 0 0 0 0 0 0 0 110.821487426758 205.431838989258 197.784744262695 165.351150512695 0 0 0 0 0 0 0 0 148.747482299805 197.384048461914 222.412368774414 130.84407043457 0 0 0 0 0 0 0 0 186.619369506836 231.119979858398 185.139663696289 0 0 0 0 0 0 0 0 129.717819213867 224.055252075195 180.142196655273 0 0 0 0 0 0 0 0 176.265243530273 223.66975402832 295.446105957031 361.572326660156 360.102111816406 244.047714233398 41.8561553955078 0 0 0 0 0 0 0 0 160.23698425293 238.57145690918 249.646987915039 191.794631958008 0 0 0 0 0 0 0 0 177.559341430664 308.058898925781 "
    "348.306823730469 288.753173828125 157.469802856445 0 0 50.9171905517578 233.195449829102 366.537780761719 333.827880859375 112.628784179688 0 0 0 0 0 0 0 0 146.29948425293 221.006973266602 185.588424682617 0 0 0 0 0 0 0 0 169.829788208008 298.886840820313 375.022705078125 349.165283203125 326.073608398438 321.16162109375 301.307739257813 280.400939941406 268.34130859375 250.299697875977 206.922592163086 161.804183959961 0 0 0 0 0 0 0 0 106.515991210938 216.509872436523 174.441055297852 0 47.4068908691406 197.185470581055 263.215942382813 352.870300292969 434.091491699219 407.799926757813 297.367797851563 209.839736938477 246.22785949707 378.100463867188 491.783874511719 505.840270996094 403.557006835938 229.298934936523 54.6394348144531 0 0 0 0 0 0 0 0 50.5400085449219 198.851058959961 280.1474609375 285.830627441406 210.564895629883 50.8290405273438 0 0 0 0 0 170.184860229492 335.466613769531 475.362731933594 547.797119140625 496.642517089844 292.609191894531 4.42018127441406 0 0 0 0 0 0 0 0 386.298461914063 549.150512695313 612.910278320313 599.075317382813 479.97314453125 260.072143554688 0 0 0 0 0 0 0 0 157.101547241211 216.543563842773 210.621841430664 151.346878051758 0 0 0 0 0 0 0 0 163.830764770508 225.50910949707 155.86100769043 0 0 0 0 0 0 0 0";

    SpectrumListSimple* sl = new SpectrumListSimple;
    SpectrumListPtr originalList(sl);
    SpectrumPtr s(new Spectrum);
    sl->spectra.push_back(s);

    vector<double> inputMZArray = parseDoubleArray(RawX);
    vector<double> inputIntensityArray = parseDoubleArray(RawY);
    s->setMZIntensityArrays(inputMZArray, inputIntensityArray, MS_number_of_detector_counts);
    s->set(MS_MSn_spectrum);
    s->set(MS_ms_level, 2);
    s->precursors.resize(1);
    s->precursors[0].activation.set(MS_electron_transfer_dissociation);
    s->precursors[0].selectedIons.resize(1);
    s->precursors[0].selectedIons[0].set(MS_selected_ion_m_z, 1000, MS_m_z);
    s->precursors[0].selectedIons[0].set(MS_charge_state, 2);

    // should be no change if we specify MS3
    SpectrumListPtr filteredList2(new 
        SpectrumList_ZeroSamplesFilter(originalList,IntegerSet(3),SpectrumList_ZeroSamplesFilter::Mode_RemoveExtraZeros,0));
    SpectrumPtr filteredSpectrum2 = filteredList2->spectrum(0, true);
    unit_assert(filteredSpectrum2->getIntensityArray()->data[9] == inputIntensityArray[9]); 


    SpectrumListPtr filteredList(new 
            SpectrumList_ZeroSamplesFilter(originalList,IntegerSet(2),SpectrumList_ZeroSamplesFilter::Mode_RemoveExtraZeros,0));

    SpectrumPtr filteredSpectrum = filteredList->spectrum(0, true);

    unit_assert(filteredSpectrum->getIntensityArray()->data.size() == filteredSpectrum->getMZArray()->data.size());
    unit_assert(filteredSpectrum->getIntensityArray()->data[0]==0);
    unit_assert(filteredSpectrum->getIntensityArray()->data[1]!=0);
    unit_assert(filteredSpectrum->getIntensityArray()->data[filteredSpectrum->getIntensityArray()->data.size()-1]==0);
    unit_assert(filteredSpectrum->getIntensityArray()->data[filteredSpectrum->getIntensityArray()->data.size()-2]!=0);
    unit_assert(filteredSpectrum->getIntensityArray()->data[1] == inputIntensityArray[9]);

    // now add missing zeros
    int nzeros=10;
    SpectrumListPtr filteredList3(new 
            SpectrumList_ZeroSamplesFilter(originalList,IntegerSet(2),SpectrumList_ZeroSamplesFilter::Mode_AddMissingZeros,nzeros));
    filteredSpectrum = filteredList3->spectrum(0, true);
    unit_assert(filteredSpectrum->getIntensityArray()->data[0]==0);
    unit_assert(filteredSpectrum->getIntensityArray()->data[1]==0);
    unit_assert(filteredSpectrum->getIntensityArray()->data[filteredSpectrum->getIntensityArray()->data.size()-1]==0);
    unit_assert(filteredSpectrum->getIntensityArray()->data[filteredSpectrum->getIntensityArray()->data.size()-2]==0);
    unit_assert(filteredSpectrum->getIntensityArray()->data[nzeros] == inputIntensityArray[9]);


}

void testIsolationWindowFilter()
{
    SpectrumListSimple* sl = new SpectrumListSimple;
    SpectrumListPtr originalList(sl);

    SpectrumPtr ms1_1(new Spectrum);
    sl->spectra.push_back(ms1_1);

    ms1_1->id = "scan=1";
    ms1_1->index = 0;
    ms1_1->setMZIntensityArrays(parseDoubleArray("1 2 3 4 5 6 7 8 9"),
                                parseDoubleArray("0 1 0 0 0 5 3 0 1"),
                                MS_number_of_detector_counts);
    ms1_1->set(MS_MSn_spectrum);
    ms1_1->set(MS_ms_level, 1);

    SpectrumPtr ms2_1(new Spectrum);
    sl->spectra.push_back(ms2_1);

    ms2_1->id = "scan=2";
    ms2_1->index = 1;
    ms2_1->setMZIntensityArrays(parseDoubleArray("1 2 3 4 5 6 7 8 9"),
                                parseDoubleArray("1 1 1 1 1 1 1 1 1"),
                                MS_number_of_detector_counts);
    ms2_1->set(MS_MSn_spectrum);
    ms2_1->set(MS_ms_level, 2);
    ms2_1->precursors.resize(1);
    ms2_1->precursors[0].spectrumID = "scan=1";
    ms2_1->precursors[0].isolationWindow.set(MS_isolation_window_target_m_z, 2);
    ms2_1->precursors[0].isolationWindow.set(MS_isolation_window_lower_offset, 1);
    ms2_1->precursors[0].isolationWindow.set(MS_isolation_window_upper_offset, 1);

    SpectrumPtr ms2_2(new Spectrum);
    sl->spectra.push_back(ms2_2);

    ms2_2->id = "scan=3";
    ms2_2->index = 2;
    ms2_2->setMZIntensityArrays(parseDoubleArray("1 2 3 4 5 6 7 8 9"),
                                parseDoubleArray("1 1 1 1 1 1 1 1 1"),
                                MS_number_of_detector_counts);
    ms2_2->set(MS_MSn_spectrum);
    ms2_2->set(MS_ms_level, 2);
    ms2_2->precursors.resize(1);
    ms2_2->precursors[0].spectrumID = "scan=1";
    ms2_2->precursors[0].isolationWindow.set(MS_isolation_window_target_m_z, 6);
    ms2_2->precursors[0].isolationWindow.set(MS_isolation_window_lower_offset, 1);
    ms2_2->precursors[0].isolationWindow.set(MS_isolation_window_upper_offset, 2);

    SpectrumPtr ms1_2(new Spectrum);
    sl->spectra.push_back(ms1_2);

    ms1_2->id = "scan=4";
    ms1_2->index = 3;
    ms1_2->setMZIntensityArrays(parseDoubleArray("1 2 3 4 5 6 7 8 9"),
                                parseDoubleArray("0 1 0 1 2 1 0 1 0"),
                                MS_number_of_detector_counts);
    ms1_2->set(MS_MSn_spectrum);
    ms1_2->set(MS_ms_level, 1);

    SpectrumPtr ms2_3(new Spectrum);
    sl->spectra.push_back(ms2_3);

    ms2_3->id = "scan=5";
    ms2_3->index = 4;
    ms2_3->setMZIntensityArrays(parseDoubleArray("1 2 3 4 5 6 7 8 9"),
                                parseDoubleArray("1 1 1 1 1 1 1 1 1"),
                                MS_number_of_detector_counts);
    ms2_3->set(MS_MSn_spectrum);
    ms2_3->set(MS_ms_level, 2);
    ms2_3->precursors.resize(1);
    ms2_3->precursors[0].spectrumID = "scan=4";
    ms2_3->precursors[0].isolationWindow.set(MS_isolation_window_target_m_z, 4); // 2 3 4 5 6
    ms2_3->precursors[0].isolationWindow.set(MS_isolation_window_lower_offset, 2);
    ms2_3->precursors[0].isolationWindow.set(MS_isolation_window_upper_offset, 2);

    SpectrumPtr ms2_4(new Spectrum);
    sl->spectra.push_back(ms2_4);

    ms2_4->id = "scan=6";
    ms2_4->index = 5;
    ms2_4->setMZIntensityArrays(parseDoubleArray("1 2 3 4 5 6 7 8 9"),
                                parseDoubleArray("1 1 1 1 1 1 1 1 1"),
                                MS_number_of_detector_counts);
    ms2_4->set(MS_MSn_spectrum);
    ms2_4->set(MS_ms_level, 2);
    ms2_4->precursors.resize(1);
    ms2_4->precursors[0].spectrumID = "scan=4";
    ms2_4->precursors[0].isolationWindow.set(MS_isolation_window_target_m_z, 6); // 4 5 6 7 8 (test that overlapping windows work as expected)
    ms2_4->precursors[0].isolationWindow.set(MS_isolation_window_lower_offset, 2);
    ms2_4->precursors[0].isolationWindow.set(MS_isolation_window_upper_offset, 2);

    SpectrumDataFilterPtr filterPtr(new IsolationWindowFilter(2, originalList));
    SpectrumListPtr filteredList(new SpectrumList_PeakFilter(originalList, filterPtr));
    SpectrumPtr s1 = filteredList->spectrum(0, true);
    unit_assert_operator_equal(~parseDoubleArray("1 2 3 5 6 7 8"), (vector<double>) s1->getMZArray()->data);
    unit_assert_operator_equal(~parseDoubleArray("0 1 0 0 5 3 0"), (vector<double>) s1->getIntensityArray()->data);

    SpectrumPtr s2 = filteredList->spectrum(3, true);
    unit_assert_operator_equal(~parseDoubleArray("2 3 4 5 6 7 8"), (vector<double>) s2->getMZArray()->data);
    unit_assert_operator_equal(~parseDoubleArray("1 0 1 2 1 0 1"), (vector<double>) s2->getIntensityArray()->data);
}
    

void test()
{
    testIntensityThresholding();
    testPrecursorMassRemoval();
    testDeisotoping();
    testMS2Denoising();
    testZeroSamplesFilter();
    testIsolationWindowFilter();
}

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
