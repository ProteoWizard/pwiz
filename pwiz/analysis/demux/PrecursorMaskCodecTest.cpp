//
// PrecursorMaskCodecTest.cpp
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

#include "pwiz/analysis/demux/PrecursorMaskCodec.hpp"
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/analysis/demux/DemuxTestData.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::msdata;

class PrecursorMaskCodecTest {

public:

    void Run()
    {
        SetUp();

        SingleOverlapTest();
        TooFewSpectraToDetermineTheNumberOfPrecursorWindowsErrorTest();
        NumberOfPrecursorsIsVaryingBetweenIndividualMS2ScansErrorTest();
        MS2SpectrumIsMissingPrecursorInformationErrorTest();
        NoMS2ScansFoundForThisExperimentErrorTest();

        TearDown();
    }

protected:

    virtual void SetUp()
    {
    }

    void TearDown()
    {
    }

    void PrecursorMaskCodecDummyInitialize(SpectrumList_const_ptr slPtr, bool variableFill = false)
    {
        PrecursorMaskCodec::Params params;
        params.variableFill = variableFill;
        PrecursorMaskCodec pmc(slPtr, params);
    }

    // Remember which spectra correspond to what states
    const int MS1_INDEX_0 = 0;
    const int MS2_INDEX_0 = 1;
    const int MS1_INDEX_1 = 2;
    const int MS2_INDEX_1 = 3;
    const int MS1_INDEX_2 = 4;

    void TooFewSpectraToDetermineTheNumberOfPrecursorWindowsErrorTest()
    {
        // Generate test data
        MSDataPtr msd = boost::make_shared<MSData>();
        examples::initializeTiny(*msd);
        auto spectrumListPtr = msd->run.spectrumListPtr;

        // This should fail to read through the example dataset because there are too few spectra to interpret an acquisition scheme
        unit_assert_throws_what(PrecursorMaskCodecDummyInitialize(spectrumListPtr, false), runtime_error,
            "IdentifyCycle() Could not determine demultiplexing scheme. Too few spectra to determine the number of precursor windows.");
        unit_assert_throws_what(PrecursorMaskCodecDummyInitialize(spectrumListPtr, true), runtime_error,
            "IdentifyCycle() Could not determine demultiplexing scheme. Too few spectra to determine the number of precursor windows.");
    }

    void NumberOfPrecursorsIsVaryingBetweenIndividualMS2ScansErrorTest()
    {
        // Generate test data
        MSDataPtr msd = boost::make_shared<MSData>();
        examples::initializeTiny(*msd);
        auto spectrumListPtr = msd->run.spectrumListPtr;

        // Make the number of precursors > 1 but allow the number of precursors to vary
        msd = boost::make_shared<MSData>();
        examples::initializeTiny(*msd);
        spectrumListPtr = msd->run.spectrumListPtr;
        Spectrum& s20 = *spectrumListPtr->spectrum(MS2_INDEX_1);
        s20.precursors.resize(2);
        Precursor& precursor = s20.precursors.back();
        precursor.spectrumID = s20.id;
        precursor.isolationWindow.set(MS_isolation_window_target_m_z, 455.3, MS_m_z);
        precursor.isolationWindow.set(MS_isolation_window_lower_offset, .5, MS_m_z);
        precursor.isolationWindow.set(MS_isolation_window_upper_offset, .5, MS_m_z);
        precursor.selectedIons.resize(1);
        precursor.selectedIons[0].set(MS_selected_ion_m_z, 455.34, MS_m_z);
        precursor.selectedIons[0].set(MS_peak_intensity, 120505, MS_number_of_detector_counts);
        precursor.selectedIons[0].set(MS_charge_state, 2);
        precursor.activation.set(MS_collision_induced_dissociation);
        precursor.activation.set(MS_collision_energy, 35.00, UO_electronvolt);

        /* The number of precursors for each spectrum cannot change between spectra and cannot be empty (Mathematically, this might be
        * allowable for demultiplexing but the code makes some simplifying assumptions that rely on these two being true)
        */

        // This should fail to read through the example dataset because the number of precursors changes between MS2 spectra
        unit_assert_throws_what(PrecursorMaskCodecDummyInitialize(spectrumListPtr, false), runtime_error,
            "IdentifyCycle() Number of precursors is varying between individual MS2 scans. Cannot infer demultiplexing scheme.");
        unit_assert_throws_what(PrecursorMaskCodecDummyInitialize(spectrumListPtr, true), runtime_error,
            "IdentifyCycle() Number of precursors is varying between individual MS2 scans. Cannot infer demultiplexing scheme.");
    }

    void MS2SpectrumIsMissingPrecursorInformationErrorTest()
    {
        // Generate test data
        MSDataPtr msd = boost::make_shared<MSData>();
        examples::initializeTiny(*msd);
        auto spectrumListPtr = msd->run.spectrumListPtr;

        // Make the number of precursors for the first MS2 spectrum equal to zero
        msd = boost::make_shared<MSData>();
        examples::initializeTiny(*msd);
        spectrumListPtr = msd->run.spectrumListPtr;
        spectrumListPtr->spectrum(MS2_INDEX_0)->precursors.clear();

        // This should fail to read through the example dataset because the first MS2 spectrum is empty
        unit_assert_throws_what(PrecursorMaskCodecDummyInitialize(spectrumListPtr, false), runtime_error,
            "IdentifyCycle() MS2 spectrum is missing precursor information.");
        unit_assert_throws_what(PrecursorMaskCodecDummyInitialize(spectrumListPtr, true), runtime_error,
            "IdentifyCycle() MS2 spectrum is missing precursor information.");
    }

    void NoMS2ScansFoundForThisExperimentErrorTest()
    {
        // Generate test data
        MSDataPtr msd = boost::make_shared<MSData>();
        examples::initializeTiny(*msd);
        auto spectrumListPtr = msd->run.spectrumListPtr;

        // Remove all MS2 spectra
        shared_ptr<SpectrumListSimple> spectrumListSimple(new SpectrumListSimple);
        spectrumListSimple->dp = boost::make_shared<DataProcessing>(*spectrumListPtr->dataProcessingPtr());
        spectrumListSimple->spectra.push_back(spectrumListPtr->spectrum(MS1_INDEX_0));
        spectrumListSimple->spectra.push_back(spectrumListPtr->spectrum(MS1_INDEX_1));
        spectrumListSimple->spectra.push_back(spectrumListPtr->spectrum(MS1_INDEX_2));
        msd->run.spectrumListPtr = spectrumListSimple;
        spectrumListPtr = msd->run.spectrumListPtr;

        // This should fail because there are no MS2 spectra in the list
        unit_assert_throws_what(PrecursorMaskCodecDummyInitialize(spectrumListPtr, false), runtime_error,
            "IdentifyCycle() No MS2 scans found for this experiment.");
        unit_assert_throws_what(PrecursorMaskCodecDummyInitialize(spectrumListPtr, true), runtime_error,
            "IdentifyCycle() No MS2 scans found for this experiment.");
    }

    // Test non-multiplexed spectra. This should return the input spectra unchanged, but it would also be acceptable to just return an error to limit code complexity.
    // TODO This feature would need to be added
    void BasicDIATest()
    {
        //TODO
    }

    // Test the simple case where we have a single overlap per spectrum. This is what Jarrett's published method describes.
    void SingleOverlapTest()
    {
        MSDataPtr msd = boost::make_shared<MSData>();
        auto paramFactory = []()
        {
            test::SimulatedDemuxParams params;
            params.numCycles = 5;
            params.numMs2ScansPerCycle = 25;
            params.numOverlaps = 1;
            params.numPrecursorsPerSpectrum = 1;
            return params;
        };

        const test::SimulatedDemuxParams params = paramFactory();
        test::initializeMSDDemux(*msd, params);
        auto spectrumListPtr = msd->run.spectrumListPtr;

        PrecursorMaskCodec::Params pmcParams;
        pmcParams.variableFill = false;
        PrecursorMaskCodec pmc(spectrumListPtr, pmcParams);
        unit_assert_operator_equal(pmc.GetOverlapsPerCycle(), params.numOverlaps + 1);
        unit_assert_operator_equal(pmc.GetOverlapsPerCycle(), 2);
        unit_assert_operator_equal(pmc.GetPrecursorsPerSpectrum(), params.numPrecursorsPerSpectrum);
        unit_assert_operator_equal(pmc.GetPrecursorsPerSpectrum(), 1);
        unit_assert_operator_equal(pmc.GetSpectraPerCycle(), params.numMs2ScansPerCycle * (params.numOverlaps + 1));
        unit_assert_operator_equal(pmc.GetSpectraPerCycle(), 50);
        unit_assert_operator_equal(pmc.GetNumDemuxWindows(), params.numMs2ScansPerCycle * (params.numOverlaps + 1) + 1);
        unit_assert_operator_equal(pmc.GetNumDemuxWindows(), 51);

        // Test ability to demultiplex by generating data from the same elution scheme with twice the selectivity and then compare to the demultiplexed data
        // TODO
    }

    // Test where there are two overlaps per spectrum
    // TODO This feature would need to be added
    void DoubleOverlapTest()
    {
        //TODO Extend or copy the code from the single overlap test in order to validate this test.
        /* Note: It may be necessary to allow for more noise in the reconstruction accuracy (when comparing the theoretical high-selectivity spectra
         * to the demultiplexed spectra). Double overlapping hasn't been tested before and may give poorer performance than single overlapping
         */
    }


    void MSXTest()
    {
        //TODO
    }

    // Test that gaps between precursors gives an error
    void GapsBetweenPrecursorsTest()
    {
        //TODO
    }

    void VariableFillTest()
    {
        //TODO
    }

    void VaryingPrecursorWidthTest()
    {
        //TODO
    }

    void OverlapMSXTest()
    {
        //TODO
    }

    void WatersSONARTest()
    {
        //TODO
    }

};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

        try
    {
        PrecursorMaskCodecTest tester;
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