//
// SpectrumPeakExtractorTest.cpp
//
//
// Original author: Austin Keller <atkeller .@. uw.edu>
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

#include "pwiz/analysis/demux/SpectrumPeakExtractor.hpp"
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "DemuxTypes.hpp"

using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::msdata;

class SpectrumPeakExtractorTest {
public:
    void Run()
    {
        SetUp();
        ExtractPeaksTest();
        TearDown();
    }

protected:

    virtual void SetUp()
    {
    }

    void TearDown()
    {
    }

    void ExtractPeaksTest()
    {
        // Generate test data
        MSData msd;
        examples::initializeTiny(msd);

        // Remember which spectra correspond to what states
        const int MS2_INDEX_0 = 1;
        const int MS2_INDEX_1 = 3;

        auto centroidedPtr = msd.run.spectrumListPtr;

        Spectrum_const_ptr s20 = centroidedPtr->spectrum(MS2_INDEX_0, true);
        SpectrumPtr s21 = centroidedPtr->spectrum(MS2_INDEX_1, true);

        // Build new mz and intensity arrays for the second spectrum
        s21->binaryDataArrayPtrs.clear();
        s21->setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
        BinaryData<double>& newMzs = s21->getMZArray()->data;
        BinaryData<double>& newIntensities = s21->getIntensityArray()->data;

        newMzs = vector<double>({ 0.0, 2.0, 2.000001, 3.999999, 4.0, 4.000001, 6.0, 8.0, 10.0, 12.0, 14.0, 16.0, 18.0 });
        for (size_t mz = 0; mz < newMzs.size(); ++mz)
        {
            newIntensities.push_back(1.0);
        }

        vector<double> s21ExpectedIntensities = { 1.0, 2.0, 3.0 };
        while (s21ExpectedIntensities.size() < s20->getIntensityArray()->data.size())
        {
            s21ExpectedIntensities.push_back(1.0);
        }

        // Create peak extractor to match mz set of the first spectrum
        BinaryDataArrayPtr mzsToDemux = s20->getMZArray();
        SpectrumPeakExtractor peakExtractor(mzsToDemux->data, pwiz::chemistry::MZTolerance(10, pwiz::chemistry::MZTolerance::PPM));

        // Test number of peaks
        unit_assert_operator_equal(peakExtractor.numPeaks(), mzsToDemux->data.size());

        // Make matrix to extract peak info into
        MatrixPtr signal;
        int numSpectra = 2;
        signal.reset(new MatrixType(numSpectra, mzsToDemux->data.size()));

        // Extract spectra
        peakExtractor(s20, *signal, 0);
        peakExtractor(s21, *signal, 1);

        // Check that self extraction returns the original spectrum
        Spectrum_const_ptr baseSpectrum = centroidedPtr->spectrum(MS2_INDEX_0, true);
        BinaryDataArrayPtr baseIntensities = baseSpectrum->getIntensityArray();
        for (size_t i = 0; i < baseIntensities->data.size(); ++i)
        {
            unit_assert_equal(signal->row(0)[i], baseIntensities->data.at(i), 0.0001);
        }

        // Check the second spectrum extraction
        for (size_t i = 0; i < s21ExpectedIntensities.size(); ++i)
        {
            unit_assert_equal(signal->row(1)[i], s21ExpectedIntensities[i], 0.0001);
        }

        // Now extract from the second spectrum, which has closely spaced peaks to simulate non-centroided data
        SpectrumPeakExtractor binExamplePeakExtractor(newMzs, pwiz::chemistry::MZTolerance(10, pwiz::chemistry::MZTolerance::PPM));
        unit_assert_operator_equal(binExamplePeakExtractor.numPeaks(), s21->getMZArray()->data.size());

        // Extract spectra
        signal.reset(new MatrixType(numSpectra, s21->getMZArray()->data.size()));
        binExamplePeakExtractor(s21, *signal, 0);
        binExamplePeakExtractor(s20, *signal, 1);

        // Check the self extraction returns the original spectrum
        for (size_t i = 0; i < s21->getIntensityArray()->data.size(); ++i)
        {
            unit_assert_equal(signal->row(0)[i], s21->getIntensityArray()->data.at(i), 0.0001);
        }
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        SpectrumPeakExtractorTest tester;
        tester.Run();
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