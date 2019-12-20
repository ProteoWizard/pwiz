//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "pwiz/utility/misc/unit.hpp"
#include "Reader_Thermo.hpp"
#include "pwiz/utility/misc/VendorReaderTestHarness.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"

#ifdef PWIZ_READER_THERMO
#include "Reader_Thermo_Detail.hpp"
#include <windows.h>
#endif

struct IsRawFile : public pwiz::util::TestPathPredicate
{
    bool operator() (const string& rawpath) const
    {
        return bal::to_lower_copy(BFS_STRING(bfs::path(rawpath).extension())) == ".raw";
    }
};

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    #if defined(PWIZ_READER_THERMO) && !defined(PWIZ_READER_THERMO_TEST_ACCEPT_ONLY)
    const bool testAcceptOnly = false;
    #else
    const bool testAcceptOnly = true;
    #endif

    try
    {
        #ifdef PWIZ_READER_THERMO

        using namespace pwiz::msdata;
        using namespace pwiz::msdata::detail;
        using namespace pwiz::msdata::detail::Thermo;
        using namespace pwiz::cv;
        using namespace pwiz::util;

        // test that all instrument types are handled by translation functions (skipping the 'Unknown' type)
        bool allInstrumentTestsPassed = true;
        for (int i=0; i < (int) InstrumentModelType_Count; ++i)
        {
            InstrumentModelType model = (InstrumentModelType) i;

            try
            {
                unit_assert(translateAsInstrumentModel(model) != CVID_Unknown);

                Component dummySource;
                vector<InstrumentConfiguration> configurations = createInstrumentConfigurations(dummySource, model);

                switch (model)
                {
                    case InstrumentModelType_Tempus_TOF:
                    case InstrumentModelType_Element_2:
                    case InstrumentModelType_Element_XR:
                    case InstrumentModelType_Element_GD:
                    case InstrumentModelType_Delta_Plus_Advantage:
                    case InstrumentModelType_Delta_Plus_XP:
                    case InstrumentModelType_Neptune:
                    case InstrumentModelType_Triton:
                        unit_assert(configurations.empty());
                        break;

                    default:
                        unit_assert(!configurations.empty());
                        break;
                }

                // test for ionization types for this instrument
                vector<IonizationType> ionizationTypes = getIonSourcesForInstrumentModel(model);

                switch (model)
                {
                    case InstrumentModelType_Element_XR:
                    case InstrumentModelType_Element_2:
                    case InstrumentModelType_Delta_Plus_Advantage:
                    case InstrumentModelType_Delta_Plus_XP:
                    case InstrumentModelType_Neptune:
                    case InstrumentModelType_Tempus_TOF:
                    case InstrumentModelType_Triton:
                    case InstrumentModelType_MAT253:
                    case InstrumentModelType_MAT900XP:
                    case InstrumentModelType_MAT900XP_Trap:
                    case InstrumentModelType_MAT95XP:
                    case InstrumentModelType_MAT95XP_Trap:
                    case InstrumentModelType_Surveyor_PDA:
                    case InstrumentModelType_Accela_PDA:
                        unit_assert(ionizationTypes.empty());
                        break;

                    default:
                        unit_assert(!ionizationTypes.empty());
                        break;
                }

                // test for mass analyzer types for this instrument
                vector<MassAnalyzerType> massAnalyzerTypes = getMassAnalyzersForInstrumentModel(model);

                switch (model)
                {
                    case InstrumentModelType_Element_XR:
                    case InstrumentModelType_Element_2:
                    case InstrumentModelType_Element_GD:
                    case InstrumentModelType_Delta_Plus_Advantage:
                    case InstrumentModelType_Delta_Plus_XP:
                    case InstrumentModelType_Neptune:
                    case InstrumentModelType_Triton:
                    case InstrumentModelType_Surveyor_PDA:
                    case InstrumentModelType_Accela_PDA:
                        unit_assert(massAnalyzerTypes.empty());
                        break;

                    default:
                        unit_assert(!massAnalyzerTypes.empty());
                        break;
                }

                // test for detector types for this instrument
                vector<DetectorType> detectorTypes = getDetectorsForInstrumentModel(model);

                switch (model)
                {
                    case InstrumentModelType_Element_GD:
                    case InstrumentModelType_Element_XR:
                    case InstrumentModelType_Element_2:
                    case InstrumentModelType_Delta_Plus_Advantage:
                    case InstrumentModelType_Delta_Plus_XP:
                    case InstrumentModelType_Neptune:
                    case InstrumentModelType_Tempus_TOF:
                    case InstrumentModelType_Triton:
                    case InstrumentModelType_MAT253:
                    case InstrumentModelType_MAT900XP:
                    case InstrumentModelType_MAT900XP_Trap:
                    case InstrumentModelType_MAT95XP:
                    case InstrumentModelType_MAT95XP_Trap:
                        unit_assert(detectorTypes.empty());
                        break;

                    default:
                        unit_assert(!detectorTypes.empty());
                        break;
                }

                // test for translation of scan filter mass analyzer type to real mass analyzer type
                BOOST_FOREACH(MassAnalyzerType realType, massAnalyzerTypes)
                {
                    bool hasCorrespondingScanFilterType = false;
                    for (int j=0; j < (int) ScanFilterMassAnalyzerType_Count; ++j)
                        if (convertScanFilterMassAnalyzer((ScanFilterMassAnalyzerType) j, model) == realType)
                            hasCorrespondingScanFilterType = true;
                    unit_assert(hasCorrespondingScanFilterType);
                }
            }
            catch (runtime_error& e)
            {
                cerr << "Unit test failed for instrument model " << lexical_cast<string>(model) << ":\n" << e.what() << endl;
                allInstrumentTestsPassed = false;
            }
        }

        unit_assert(allInstrumentTestsPassed);
        #endif

        bool requireUnicodeSupport = true;
        pwiz::msdata::Reader_Thermo reader;
        pwiz::util::ReaderTestConfig config;

        #ifndef _WIN64
        config.diffPrecision = 1e-2;
        #endif

        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsRawFile(), config);

        config.peakPicking = true;
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsRawFile(), config);

        // test globalChromatogramsAreMs1Only, but don't need to test spectra here
        auto newConfig = config;
        newConfig.globalChromatogramsAreMs1Only = true;
        newConfig.indexRange = make_pair(0, 0);
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, pwiz::util::IsNamedRawFile("090701-LTQVelos-unittest-01.raw"), newConfig);

        #ifdef PWIZ_READER_THERMO
        // CONSIDER: do this test in VendorReaderTestHarness for all vendor readers?
        ::SetThreadLocale(LANG_TURKISH);
        pwiz::util::testReader(reader, testArgs, testAcceptOnly, requireUnicodeSupport, IsRawFile(), config);
        #endif
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
