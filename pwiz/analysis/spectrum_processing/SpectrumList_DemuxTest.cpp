//
// $Id$
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

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Demux.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PeakPicker.hpp"
#include "pwiz/analysis/demux/DemuxHelpers.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/Serializer_mzML.hpp"
#include "pwiz/data/msdata/Diff.hpp"
#include "pwiz_tools/common/FullReaderList.hpp"
#include <pwiz/utility/misc/IntegerSet.hpp>
#include <boost/make_shared.hpp>

#define _VERIFY_EXACT_SPECTRUM

using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::analysis;

ostream* os_ = 0;

const size_t TEST_SPECTRUM_OVERLAP = 134;
const size_t TEST_SPECTRUM_OVERLAP_ORIGINAL = 67;
const size_t NUM_DECONV_IN_TEST_SPECTRUM_OVERLAP = 2;
const size_t TEST_SPECTRUM_OVERLAP_DEMUX_INDEX = 134;

const size_t TEST_SPECTRUM_MSX = 105;
const size_t TEST_SPECTRUM_MSX_ORIGINAL = 21;
const size_t NUM_DECONV_IN_TEST_SPECTRUM_MSX = 5;
const size_t TEST_SPECTRUM_MSX_DEMUX_INDEX = 105;

struct DemuxTest {
    typedef boost::shared_ptr<MSData> MSDataPtr;
    struct MSDPair
    {
        MSDPair(MSDataPtr msdata, SpectrumListPtr spectrumList) : msdata(msdata), spectrumList(spectrumList) {}
        MSDataPtr msdata;
        SpectrumListPtr spectrumList;
    };

    MSDPair GenerateSpectrumList(const string& inputFile,
        bool demux = false,
        const SpectrumList_Demux::Params& params = SpectrumList_Demux::Params()) const;

    void GetMask(const vector<double>& original, const vector<double>& derived, vector<size_t>& mask) const;
};

DemuxTest::MSDPair DemuxTest::GenerateSpectrumList(const string& inputFile,
    bool demux,
    const SpectrumList_Demux::Params& params) const
{
    FullReaderList readers;
    MSDataPtr msdPtr = boost::make_shared<MSDataFile>(inputFile, &readers);
    IntegerSet levelsToCentroid(1, 2);
    SpectrumListPtr centroidedPtr(
        new SpectrumList_PeakPicker(msdPtr->run.spectrumListPtr,
        PeakDetectorPtr(boost::make_shared<LocalMaximumPeakDetector>(3)),
        true,
        levelsToCentroid));
    msdPtr->filterApplied();
    
    if (!demux)
        return MSDPair(msdPtr, centroidedPtr);

    SpectrumListPtr demuxList(new SpectrumList_Demux(centroidedPtr, params));
    msdPtr->filterApplied();
    msdPtr->run.spectrumListPtr = demuxList;
    return MSDPair(msdPtr, demuxList);
}

void DemuxTest::GetMask(const vector<double>& original, const vector<double>& derived, vector<size_t>& mask) const
{
    unit_assert(std::is_sorted(original.begin(), original.end()));
    unit_assert(std::is_sorted(derived.begin(), derived.end()));
    mask.clear();
    auto originalIt = original.begin();
    for (auto derivedIt = derived.begin(); derivedIt != derived.end(); ++derivedIt)
    {
        for (; originalIt != original.end(); ++originalIt)
        {
            if (abs(*originalIt - *derivedIt) < 1.0e-5)
            {
                mask.push_back(originalIt - original.begin());
                break;
            }
            unit_assert(*originalIt < *derivedIt);
        }
    }
    unit_assert_operator_equal(derived.size(), mask.size());
}

void testOverlapOnly(const string& filepath)
{
    // Select the appropriate overlap demux file
    bfs::path overlapTestFile = filepath;

    // Create output file in the same directory
    bfs::path testOutputFile = "OverlapTestOutput.mzML";

    DemuxTest test;

    // Create reader for spectrum without demux
    auto originalSpectrumList = test.GenerateSpectrumList(overlapTestFile.string());
    SpectrumList_Demux::Params demuxParams;
    demuxParams.optimization = DemuxOptimization::OVERLAP_ONLY;
    auto demuxList = test.GenerateSpectrumList(overlapTestFile.string(), true, demuxParams);

    // Find the original spectrum for this demux spectrum
    auto demuxID = demuxList.spectrumList->spectrumIdentity(TEST_SPECTRUM_OVERLAP);
    size_t originalIndex;
    unit_assert(TryGetOriginalIndex(demuxID, originalIndex));

    {
        // Verify that the original spectrum was matched with the demux spectrum ids
        auto originalSpectrumId = originalSpectrumList.spectrumList->spectrumIdentity(TEST_SPECTRUM_OVERLAP_ORIGINAL);
        size_t originalIndexFromDemux;
        unit_assert(TryGetScanIndex(originalSpectrumId, originalIndexFromDemux));
        unit_assert_operator_equal(originalIndex, originalIndexFromDemux);
    }

    // Get original spectrum
    auto originalSpectrum = originalSpectrumList.spectrumList->spectrum(TEST_SPECTRUM_OVERLAP_ORIGINAL, true);
    auto originalMzs = originalSpectrum->getMZArray()->data;
    auto originalIntensities = originalSpectrum->getIntensityArray()->data;

    {
        // Calculate summed intensites of the demux spectra
        vector<double> peakSums(originalIntensities.size(), 0.0);
        for (size_t i = 0, demuxIndex = TEST_SPECTRUM_OVERLAP; i < NUM_DECONV_IN_TEST_SPECTRUM_OVERLAP; ++i, ++demuxIndex)
        {
            auto demuxSpectrum = demuxList.spectrumList->spectrum(demuxIndex);
            auto demuxIntensities = demuxSpectrum->getIntensityArray()->data;
            auto demuxMzs = demuxSpectrum->getMZArray()->data;

            vector<size_t> indexMask;
            test.GetMask(originalMzs, demuxMzs, indexMask);

            size_t j = 0;
            for (auto index : indexMask)
            {
                peakSums[index] += demuxIntensities.at(j++);
            }
        }

        // Verify that the demux spectra sum to the original spectrum
        for (size_t i = 0; i < peakSums.size(); ++i)
        {
            unit_assert_equal(peakSums.at(i), originalIntensities.at(i), 1e-7);
        }
    }

    // Verify that the spectrum window boundaries are set correctly
    {
        auto originalPrecursor = originalSpectrum->precursors[0];
        double originalTarget = originalPrecursor.isolationWindow.cvParam(MS_isolation_window_target_m_z).valueAs<double>();
        double originalLowerOffset = originalPrecursor.isolationWindow.cvParam(MS_isolation_window_lower_offset).valueAs<double>();
        double originalUpperOffset = originalPrecursor.isolationWindow.cvParam(MS_isolation_window_upper_offset).valueAs<double>();
        auto expectedOffset = (originalLowerOffset + originalUpperOffset) / (2.0 * static_cast<double>(NUM_DECONV_IN_TEST_SPECTRUM_OVERLAP));
        auto windowStart = originalTarget - originalLowerOffset;
        for (size_t i = 0, demuxIndex = TEST_SPECTRUM_OVERLAP; i < NUM_DECONV_IN_TEST_SPECTRUM_OVERLAP; ++i, ++demuxIndex)
        {
            double expectedTarget = windowStart + expectedOffset + 2.0 * expectedOffset * i;

            auto demuxSpectrum = demuxList.spectrumList->spectrum(demuxIndex);
            auto demuxPrecursor = demuxSpectrum->precursors[0];
            double actualTarget = demuxPrecursor.isolationWindow.cvParam(MS_isolation_window_target_m_z).valueAs<double>();
            double actualLowerOffset = demuxPrecursor.isolationWindow.cvParam(MS_isolation_window_lower_offset).valueAs<double>();
            double actualUpperOffset = demuxPrecursor.isolationWindow.cvParam(MS_isolation_window_upper_offset).valueAs<double>();

            // We expect the boundaries to vary based on the minimum window size. Adjacent boundaries
            // are merged and averaged when within this window size threshold. So we only check for agreement
            // to within this precision
            const double minimumWindowSize = 0.01;

            unit_assert_equal(expectedTarget, actualTarget, minimumWindowSize / 2.0);
            unit_assert_equal(expectedOffset, actualLowerOffset, minimumWindowSize);
            unit_assert_equal(expectedOffset, actualUpperOffset, minimumWindowSize);
        }
    }

#ifdef _VERIFY_EXACT_SPECTRUM
    // Verify that the intensity values are as expected for a demux spectrum

    // TODO These are the Skyline intensities for this spectrum in profile. These should be used for profile demux.
    /*vector<size_t> intensityIndices = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 290, 291, 292, 293, 294, 295, 296 };
    vector<double> intensityValues =
    {
        0.0, 0.0, 0.0, 0.0, 4545.85, 15660.49,
        35050.01, 56321.66, 62715.75, 43598.31, 23179.42,
        2745.94, 3870.54, 4060.16, 3148.17, 1656.38,
        0.0, 0.0
    };*/

    vector<double> intensityValues = 
    {
        62715.75,
        10856.38,
        26514.10,
        15964.11,
        35976.23,
        24815.48,
        10131.85,
        21044.27,
        34393.21,
        9127.96,
        50067.90,
        10287.26,
        11103.65,
        19305.24,
        9583.66,
        11572.70,
        9995.09,
        29599.00,
        46296.34,
        32724.88,
        9292.13,
        8167.25,
        1111.66,
        25497.61,
        23860.40,
        44635.87,
        28415.64,
        9848.89,
        18376.83,
        24337.12,
        43483.74,
        26286.20,
        40075.65
    };

    auto demuxSpectrumAbsoluteCheck = demuxList.spectrumList->spectrum(TEST_SPECTRUM_OVERLAP_DEMUX_INDEX);
    auto demuxIntensities = demuxSpectrumAbsoluteCheck->getIntensityArray()->data;
    auto demuxMzs = demuxSpectrumAbsoluteCheck->getMZArray()->data;

    // Verify intensities are equal
    unit_assert_operator_equal(demuxIntensities.size(), intensityValues.size());
    for (size_t i = 0; i < intensityValues.size(); ++i)
    {
        unit_assert_equal(demuxIntensities.at(i), intensityValues.at(i), 0.1);
    }
#endif
}

void testMSXOnly(const string& filepath)
{
    // Select the appropriate msx demux file
    bfs::path msxTestFile = filepath;

    // Create output file in the same directory
    bfs::path testOutputFile = "MsxTestOutput.mzML";

    DemuxTest test;

    // Create reader for spectrum without demux
    auto originalSpectrumList = test.GenerateSpectrumList(msxTestFile.string());
    SpectrumList_Demux::Params demuxParams;
    auto demuxList = test.GenerateSpectrumList(msxTestFile.string(), true, demuxParams);

    // Find the original spectrum for this demux spectrum
    auto demuxID = demuxList.spectrumList->spectrumIdentity(TEST_SPECTRUM_MSX);
    size_t originalIndex;
    unit_assert(TryGetOriginalIndex(demuxID, originalIndex));

    {
        // Verify that the original spectrum was matched with the demux spectrum ids
        auto originalSpectrumId = originalSpectrumList.spectrumList->spectrumIdentity(TEST_SPECTRUM_MSX_ORIGINAL);
        size_t originalIndexFromDemux;
        unit_assert(TryGetScanIndex(originalSpectrumId, originalIndexFromDemux));
        unit_assert_operator_equal(originalIndex, originalIndexFromDemux);
    }

    // Get original spectrum
    auto originalSpectrum = originalSpectrumList.spectrumList->spectrum(TEST_SPECTRUM_MSX_ORIGINAL, true);
    auto originalMzs = originalSpectrum->getMZArray()->data;
    auto originalIntensities = originalSpectrum->getIntensityArray()->data;

    {
        // Calculate summed intensites of the demux spectra
        vector<double> peakSums(originalIntensities.size(), 0.0);
        for (size_t i = 0, demuxIndex = TEST_SPECTRUM_MSX; i < NUM_DECONV_IN_TEST_SPECTRUM_MSX; ++i, ++demuxIndex)
        {
            auto demuxSpectrum = demuxList.spectrumList->spectrum(demuxIndex);
            auto demuxIntensities = demuxSpectrum->getIntensityArray()->data;
            auto demuxMzs = demuxSpectrum->getMZArray()->data;

            vector<size_t> indexMask;
            test.GetMask(originalMzs, demuxMzs, indexMask);

            size_t j = 0;
            for (auto index : indexMask)
            {
                peakSums[index] += demuxIntensities.at(j++);
            }
        }

        // Verify that the demux spectra sum to the original spectrum
        for (size_t i = 0; i < peakSums.size(); ++i)
        {
            unit_assert_equal(peakSums.at(i), originalIntensities.at(i), 1e-7);
        }
    }

    // Verify that the spectrum window boundaries are set correctly
    {
        struct SimplePrecursor
        {
            double target;
            double lowerOffset;
            double upperOffset;

            bool operator<(const SimplePrecursor& rhs) const { return this->target < rhs.target; }
        };

        vector<SimplePrecursor> originalPrecursors;
        for (auto& precursor : originalSpectrum->precursors)
        {
            SimplePrecursor p;
            p.target = precursor.isolationWindow.cvParam(MS_isolation_window_target_m_z).valueAs<double>();
            p.lowerOffset = precursor.isolationWindow.cvParam(MS_isolation_window_lower_offset).valueAs<double>();
            p.upperOffset = precursor.isolationWindow.cvParam(MS_isolation_window_upper_offset).valueAs<double>();
            originalPrecursors.push_back(p);
        }
        sort(originalPrecursors.begin(), originalPrecursors.end());
        
        for (size_t i = 0, demuxIndex = TEST_SPECTRUM_MSX; i < NUM_DECONV_IN_TEST_SPECTRUM_MSX; ++i, ++demuxIndex)
        {
            const auto& originalPrecursor = originalPrecursors.at(i);

            auto demuxSpectrum = demuxList.spectrumList->spectrum(demuxIndex);
            auto demuxPrecursor = demuxSpectrum->precursors[0];
            double actualTarget = demuxPrecursor.isolationWindow.cvParam(MS_isolation_window_target_m_z).valueAs<double>();
            double actualLowerOffset = demuxPrecursor.isolationWindow.cvParam(MS_isolation_window_lower_offset).valueAs<double>();
            double actualUpperOffset = demuxPrecursor.isolationWindow.cvParam(MS_isolation_window_upper_offset).valueAs<double>();

            // We expect the boundaries to vary based on the minimum window size. Adjacent boundaries
            // are merged and averaged when within this window size threshold. So we only check for agreement
            // to within this precision
            const double minimumWindowSize = 0.01;

            unit_assert_equal(originalPrecursor.target, actualTarget, minimumWindowSize / 2.0);
            unit_assert_equal(originalPrecursor.lowerOffset, actualLowerOffset, minimumWindowSize);
            unit_assert_equal(originalPrecursor.upperOffset, actualUpperOffset, minimumWindowSize);
        }
    }

#ifdef _VERIFY_EXACT_SPECTRUM
    // Verify that the intensity values are as expected for a demux spectrum

    // TODO These are the Skyline intensities for this spectrum in profile. These should be used for profile demux.
    /*vector<double> intensityValues =
    {
        0.0, 0.0, 0.0, 0.0, 142.95, 349.75,
        542.87, 511.77, 248.4, 0.0, 49.28,
        1033.65, 278.56, 0.0, 0.0, 0.0,
        0.0, 0.0
    };*/

    vector<double> intensityValues =
    {
        931.31,
        550.11,
        650.53,
        1870.50,
        62.58,
        2767.20,
        4917.47,
        1525.37,
        923.80,
        726.35,
        1421.49,
        1699.59,
        3126.18,
        25833.26,
        23554.24,
        10017.21,
        900.55,
        26146.96,
        9478.34,
        2643.12,
        5988.79,
        1562.70,
        1952.92,
        1392.36,
        1354.70,
        5745.34,
        1891.37,
        2545.78,
        4131.52
    };

    auto demuxSpectrumAbsoluteCheck = demuxList.spectrumList->spectrum(TEST_SPECTRUM_MSX_DEMUX_INDEX);
    auto demuxIntensities = demuxSpectrumAbsoluteCheck->getIntensityArray()->data;
    auto demuxMzs = demuxSpectrumAbsoluteCheck->getMZArray()->data;

    // Verify intensities are equal
    unit_assert_operator_equal(demuxIntensities.size(), intensityValues.size());
    for (size_t i = 0; i < intensityValues.size(); ++i)
    {
        unit_assert_equal(demuxIntensities.at(i), intensityValues.at(i), 0.1);
    }
#endif
}


void parseArgs(const vector<string>& args, vector<string>& rawpaths)
{
    for (size_t i = 1; i < args.size(); ++i)
    {
        if (args[i] == "-v") os_ = &cout;
        else if (bal::starts_with(args[i], "--")) continue;
        else rawpaths.push_back(args[i]);
    }
}


int main(int argc, char* argv[])
{

    TEST_PROLOG(argc, argv)

    try
    {
        vector<string> demuxTestArgs(argv, argv + argc);
        vector<string> rawpaths;
        parseArgs(demuxTestArgs, rawpaths);

        ExtendedReaderList readerList;

        bool msxTested = false;
        bool overlapTested = false;

        BOOST_FOREACH(const string& filepath, rawpaths)
        {
            if (bal::ends_with(filepath, "MsxTest.mzML"))
            {
                testMSXOnly(filepath);
                msxTested = true;
            }
            else if (bal::ends_with(filepath, "OverlapTest.mzML"))
            {
                testOverlapOnly(filepath);
                overlapTested = true;
            }
        }
        unit_assert(msxTested);
        unit_assert(overlapTested);
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
