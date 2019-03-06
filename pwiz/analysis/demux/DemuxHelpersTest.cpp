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

#include "pwiz/analysis/demux/DemuxHelpers.hpp"
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "DemuxTypes.hpp"

using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::msdata;

class DemuxHelpersTest {
public:

    void Run()
    {
        SetUp();
        TryGetScanIDTokenTest();
        TryGetScanIndexTest();
        TryGetDemuxIndexTest();
        TryGetOriginalIndexTest();
        TryGetMSLevelTest();
        TryGetNumPrecursorsTest();
        TryGetStartTimeTest();
        FindNearbySpectraTest();
        TearDown();
    }

protected:

    virtual void SetUp()
    {
        // Generate test data
        examples::initializeTiny(_msd);

        auto spectrumListPtr = _msd.run.spectrumListPtr;

        _s10 = spectrumListPtr->spectrum(MS1_INDEX_0, true);
        _s20 = spectrumListPtr->spectrum(MS2_INDEX_0, true);
        _s11 = spectrumListPtr->spectrum(MS1_INDEX_1, true);
        _s21 = spectrumListPtr->spectrum(MS2_INDEX_1, true);
    }

    void TearDown()
    {
    }

    void TryGetScanIDTokenTest()
    {
        string value;
        bool success;
        success = TryGetScanIDToken(*_s10, "blah", value);
        unit_assert(!success);
        success = TryGetScanIDToken(*_s10, "scan", value);
        unit_assert(success);
        unit_assert(boost::iequals(value, "19"));
    }

    void initializeLarge(MSData& msd, size_t cycleSize = 4, size_t numCycles = 5)
    {
        // Use initializeTiny as base and overwrite the spectrumList
        examples::initializeTiny(msd);

        // Pull pointers from existing spectra
        shared_ptr<SpectrumListSimple> spectrumListPtr = boost::dynamic_pointer_cast<SpectrumListSimple>(msd.run.spectrumListPtr);
        if (!spectrumListPtr)
            throw std::runtime_error("[DemuxHelpersTest::initializeLarge] spectrumList from initializeTiny was not of the expected type");

        auto dppwiz = spectrumListPtr->dp;

        auto pg1 = spectrumListPtr->spectra[0]->paramGroupPtrs[0];
        auto pg2 = spectrumListPtr->spectra[1]->paramGroupPtrs[0];

        auto instrumentConfigurationPtr = spectrumListPtr->spectra[0]->scanList.scans.back().instrumentConfigurationPtr;

        auto dpCompassXtract = spectrumListPtr->spectra[0]->binaryDataArrayPtrs[0]->dataProcessingPtr;

        // Clear spectra
        spectrumListPtr->spectra.clear();

        size_t scanNum = 0;
        for (size_t cycleIndex = 0; cycleIndex < numCycles; ++cycleIndex)
        {
            // Add MS1 spectrum first
            spectrumListPtr->spectra.push_back(SpectrumPtr(new Spectrum));
            Spectrum& ms1 = *spectrumListPtr->spectra[scanNum];
            boost::format scanfmt("scan=%1%");
            scanfmt % scanNum;
            ms1.id = scanfmt.str();
            ms1.index = scanNum;

            ms1.set(MS_ms_level, 1);

            ms1.set(MS_centroid_spectrum);
            ms1.set(MS_lowest_observed_m_z, 400.39, MS_m_z);
            ms1.set(MS_highest_observed_m_z, 1795.56, MS_m_z);
            ms1.set(MS_base_peak_m_z, 445.347, MS_m_z);
            ms1.set(MS_base_peak_intensity, 120053, MS_number_of_detector_counts);
            ms1.set(MS_total_ion_current, 1.66755e+007);

            ms1.paramGroupPtrs.push_back(pg1);
            ms1.scanList.scans.push_back(Scan());
            ms1.scanList.set(MS_no_combination);
            Scan& ms1scan = ms1.scanList.scans.back();
            ms1scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
            ms1scan.set(MS_scan_start_time, 5.890500, UO_minute);
            ms1scan.set(MS_filter_string, "+ c NSI Full ms [ 400.00-1800.00]");
            ms1scan.set(MS_preset_scan_configuration, 3);
            ms1scan.scanWindows.resize(1);
            ScanWindow& window = ms1.scanList.scans.back().scanWindows.front();
            window.set(MS_scan_window_lower_limit, 400.000000, MS_m_z);
            window.set(MS_scan_window_upper_limit, 1800.000000, MS_m_z);

            BinaryDataArrayPtr ms1_mz(new BinaryDataArray);
            ms1_mz->dataProcessingPtr = dpCompassXtract;
            ms1_mz->set(MS_m_z_array, "", MS_m_z);
            ms1_mz->data.resize(15);
            for (int i = 0; i < 15; i++)
                ms1_mz->data[i] = i;

            BinaryDataArrayPtr ms1_intensity(new BinaryDataArray);
            ms1_intensity->dataProcessingPtr = dpCompassXtract;
            ms1_intensity->set(MS_intensity_array, "", MS_number_of_detector_counts);
            ms1_intensity->data.resize(15);
            for (int i = 0; i < 15; i++)
                ms1_intensity->data[i] = 15 - i;

            ms1.binaryDataArrayPtrs.push_back(ms1_mz);
            ms1.binaryDataArrayPtrs.push_back(ms1_intensity);
            ms1.defaultArrayLength = ms1_mz->data.size();

            // Increment scan index
            ++scanNum;

            // Add MS2 spectra
            for (size_t ms2Index = 0; ms2Index < cycleSize; ++ms2Index)
            {
                spectrumListPtr->spectra.push_back(SpectrumPtr(new Spectrum));
                Spectrum& ms2 = *spectrumListPtr->spectra[scanNum];

                // Fill in MS2 data
                boost::format scanfmt("scan=%1%");
                scanfmt % scanNum;
                ms2.id = scanfmt.str();
                ms2.index = 1;

                ms2.paramGroupPtrs.push_back(pg2);
                ms2.set(MS_ms_level, 2);

                ms2.set(MS_profile_spectrum);
                ms2.set(MS_lowest_observed_m_z, 320.39, MS_m_z);
                ms2.set(MS_highest_observed_m_z, 1003.56, MS_m_z);
                ms2.set(MS_base_peak_m_z, 456.347, MS_m_z);
                ms2.set(MS_base_peak_intensity, 23433, MS_number_of_detector_counts);
                ms2.set(MS_total_ion_current, 1.66755e+007);

                ms2.precursors.resize(1);
                Precursor& precursor = ms2.precursors.front();
                precursor.spectrumID = ms1.id;
                precursor.isolationWindow.set(MS_isolation_window_target_m_z, 445.3, MS_m_z);
                precursor.isolationWindow.set(MS_isolation_window_lower_offset, .5, MS_m_z);
                precursor.isolationWindow.set(MS_isolation_window_upper_offset, .5, MS_m_z);
                precursor.selectedIons.resize(1);
                precursor.selectedIons[0].set(MS_selected_ion_m_z, 445.34, MS_m_z);
                precursor.selectedIons[0].set(MS_peak_intensity, 120053, MS_number_of_detector_counts);
                precursor.selectedIons[0].set(MS_charge_state, 2);
                precursor.activation.set(MS_collision_induced_dissociation);
                precursor.activation.set(MS_collision_energy, 35.00, UO_electronvolt);

                ms2.scanList.scans.push_back(Scan());
                ms2.scanList.set(MS_no_combination);
                Scan& ms2scan = ms2.scanList.scans.back();
                ms2scan.instrumentConfigurationPtr = instrumentConfigurationPtr;
                ms2scan.set(MS_scan_start_time, 5.990500, UO_minute);
                ms2scan.set(MS_filter_string, "+ c d Full ms2  445.35@cid35.00 [ 110.00-905.00]");
                ms2scan.set(MS_preset_scan_configuration, 4);
                ms2scan.scanWindows.resize(1);
                ScanWindow& window2 = ms2scan.scanWindows.front();
                window2.set(MS_scan_window_lower_limit, 110.000000, MS_m_z);
                window2.set(MS_scan_window_upper_limit, 905.000000, MS_m_z);

                BinaryDataArrayPtr ms2_mz(new BinaryDataArray);
                ms2_mz->dataProcessingPtr = dpCompassXtract;
                ms2_mz->set(MS_m_z_array, "", MS_m_z);
                ms2_mz->data.resize(10);
                for (int i = 0; i < 10; i++)
                    ms2_mz->data[i] = i * 2;

                BinaryDataArrayPtr ms2_intensity(new BinaryDataArray);
                ms2_intensity->dataProcessingPtr = dpCompassXtract;
                ms2_intensity->set(MS_intensity_array, "", MS_number_of_detector_counts);
                ms2_intensity->data.resize(10);
                for (int i = 0; i < 10; i++)
                    ms2_intensity->data[i] = (10 - i) * 2;

                ms2.binaryDataArrayPtrs.push_back(ms2_mz);
                ms2.binaryDataArrayPtrs.push_back(ms2_intensity);
                ms2.defaultArrayLength = ms2_mz->data.size();

                // Increment scan index
                ++scanNum;
            }
        }
    }

    void TryGetScanIndexTest()
    {
        bool success;
        size_t index;
        success = TryGetScanIndex(*_s10, index);
        unit_assert(success);
        unit_assert_operator_equal(index, 19);
        Spectrum emptySpectrum;
        success = TryGetScanIndex(emptySpectrum, index);
        unit_assert(!success);
    }

    void TryGetDemuxIndexTest()
    {
        bool success;
        size_t index = 0;
        success = TryGetDemuxIndex(*_s10, index);
        unit_assert(!success);
        Spectrum emptySpectrum;
        emptySpectrum.id = emptySpectrum.id + " originalScan=19 demux=0 scan=2";
        success = TryGetDemuxIndex(emptySpectrum, index);
        unit_assert(success);
        unit_assert_operator_equal(index, 0);
    }

    void TryGetOriginalIndexTest()
    {
        bool success;
        size_t index;
        Spectrum emptySpectrum;
        emptySpectrum.id = emptySpectrum.id + " originalScan=19 demux=0 scan=2";
        success = TryGetOriginalIndex(emptySpectrum, index);
        unit_assert(success);
        unit_assert_operator_equal(index, 19);
        emptySpectrum = Spectrum();
        success = TryGetOriginalIndex(emptySpectrum, index);
        unit_assert(!success);
    }

    void TryGetMSLevelTest()
    {
        Spectrum emptySpectrum;

        int msLevel = 0;
        bool success;
        success = TryGetMSLevel(*_s10, msLevel);
        unit_assert_operator_equal(msLevel, 1);
        unit_assert(success);
        success = TryGetMSLevel(*_s20, msLevel);
        unit_assert(success);
        unit_assert_operator_equal(msLevel, 2);
        success = TryGetMSLevel(emptySpectrum, msLevel);
        unit_assert(!success);
    }

    void TryGetNumPrecursorsTest()
    {
        bool success;
        int numPrecursors;
        Spectrum emptySpectrum;
        success = TryGetNumPrecursors(emptySpectrum, numPrecursors);
        unit_assert(!success);
        success = TryGetNumPrecursors(*_s10, numPrecursors);
        unit_assert(success);
        unit_assert_operator_equal(numPrecursors, 0);
        success = TryGetNumPrecursors(*_s20, numPrecursors);
        unit_assert(success);
        unit_assert_operator_equal(numPrecursors, 1);
    }

    void TryGetStartTimeTest()
    {
        bool success;
        double startTime;
        success = TryGetStartTime(*_s10, startTime);
        unit_assert(success);
        unit_assert_equal(startTime, 5.890500, 0.000001);
        Spectrum emptySpectrum;
        success = TryGetStartTime(emptySpectrum, startTime);
        unit_assert(!success);
    }

    void FindNearbySpectraTest()
    {
        bool success;
        vector<size_t> spectraIndices;

        // Test when centered on the first index
        success = FindNearbySpectra(spectraIndices, _msd.run.spectrumListPtr, MS2_INDEX_0, 2);
        unit_assert(success);
        unit_assert_operator_equal(2, spectraIndices.size());
        unit_assert_operator_equal(MS2_INDEX_0, spectraIndices[0]);
        unit_assert_operator_equal(MS2_INDEX_1, spectraIndices[1]);

        // Test when centered on the last index
        success = FindNearbySpectra(spectraIndices, _msd.run.spectrumListPtr, MS2_INDEX_1, 2);
        unit_assert(success);
        unit_assert_operator_equal(2, spectraIndices.size());
        unit_assert_operator_equal(MS2_INDEX_0, spectraIndices[0]);
        unit_assert_operator_equal(MS2_INDEX_1, spectraIndices[1]);

        // Test requesting only one spectrum
        success = FindNearbySpectra(spectraIndices, _msd.run.spectrumListPtr, MS2_INDEX_0, 1);
        unit_assert(success);
        unit_assert_operator_equal(1, spectraIndices.size());
        unit_assert_operator_equal(MS2_INDEX_0, spectraIndices[0]);

        // Try and request more spectra than are available
        success = FindNearbySpectra(spectraIndices, _msd.run.spectrumListPtr, MS2_INDEX_0, 3);
        unit_assert(!success);

        // Try using center index that is not an MS2 spectrum
        unit_assert_throws_what(
            success = FindNearbySpectra(spectraIndices, _msd.run.spectrumListPtr, MS1_INDEX_0, 2),
            std::runtime_error,
            "Center index must be an MS2 spectrum")

            // Test accessing out of range
            size_t tooLargeOfIndex = _msd.run.spectrumListPtr->size() + 1;
        unit_assert_throws_what(
            success = FindNearbySpectra(spectraIndices, _msd.run.spectrumListPtr, tooLargeOfIndex, 2),
            std::out_of_range,
            "Spectrum index not in range of the given spectrum list")

            // Generate a larger MSData set for testing stride and larger numbers of nearby spectra
            MSData msd;
        size_t cycleSize = 4;
        size_t numCycles = 5;
        initializeLarge(msd, cycleSize, numCycles);

        // Test for different numbers of spectra

        // Test ability to handle interspersed MS1 spectra
        size_t centerIndex = 2 * (cycleSize + 1); // Start at the beginning of the third cycle
        centerIndex += 1; // Skip MS1 spectrum
        success = FindNearbySpectra(spectraIndices, msd.run.spectrumListPtr, centerIndex, 3);
        unit_assert(success);
        unit_assert_operator_equal(3, spectraIndices.size());
        unit_assert_operator_equal(11, centerIndex); // for clarity, verify that we have the right center index
        unit_assert_operator_equal(9, spectraIndices[0]); // one ms2 behind center
        // index 10 is the ms1
        unit_assert_operator_equal(11, spectraIndices[1]); // center ms2
        unit_assert_operator_equal(12, spectraIndices[2]); // one ms2 after center

        // Test stride
        success = FindNearbySpectra(spectraIndices, msd.run.spectrumListPtr, centerIndex, 5, cycleSize);
        unit_assert(success);
        unit_assert_operator_equal(5, spectraIndices.size());
        unit_assert_operator_equal(1, spectraIndices[0]);
        unit_assert_operator_equal(6, spectraIndices[1]);
        unit_assert_operator_equal(11, spectraIndices[2]);
        unit_assert_operator_equal(16, spectraIndices[3]);
        unit_assert_operator_equal(21, spectraIndices[4]);
    }

    // Remember which spectra correspond to what states
    const size_t MS1_INDEX_0 = 0;
    const size_t MS2_INDEX_0 = 1;
    const size_t MS1_INDEX_1 = 2;
    const size_t MS2_INDEX_1 = 3;

    MSData _msd;

    Spectrum_const_ptr _s10;
    Spectrum_const_ptr _s20;
    Spectrum_const_ptr _s11;
    Spectrum_const_ptr _s21;

};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        DemuxHelpersTest tester;
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