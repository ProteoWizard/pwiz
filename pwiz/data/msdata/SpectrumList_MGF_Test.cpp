//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "SpectrumList_MGF.hpp"
#include "TextWriter.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;


ostream* os_ = 0;

const char* testMGF =
"BEGIN IONS\n"
"PEPMASS=810.790000\n"
"TITLE=small.pwiz.0003.0003.2\n"
"231.388840 26.545113\n"
"233.339828 20.447954\n"
"239.396149 17.999159\n"
"END IONS\n"
"BEGIN IONS\n"
"PEPMASS=837.340000\n"
"TITLE=small.pwiz.0004.0004.2\n"
"RTINSECONDS=123.456\n"
"CHARGE=2+\n"
"236.047043 11.674493\n"
"237.237091 24.431984\n"
"238.824036 10.019409\n"
"239.531403 6.842983\n"
"243.128693 89.586212\n"
"END IONS\n"
"BEGIN IONS\n"
"PEPMASS=123.45\n"
"TITLE=small.pwiz.0005.0005.2\n"
"RTINSECONDS=234.56\n"
"CHARGE=2- and 3-\n"
"236.047043 11.674493\n"
"237.237091 24.431984\n"
"238.824036 10.019409\n"
"239.531403 6.842983\n"
"243.128693 89.586212\n"
"END IONS\n";

void test()
{
    if (os_) *os_ << "test()\n";

    if (os_) *os_ << "mgf:\n" << testMGF << endl;

    shared_ptr<istream> is(new istringstream(testMGF));

    // dummy would normally be read in from file

    MSData dummy;
    dummy.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration("LCQDeca")));
    dummy.instrumentConfigurationPtrs.back()->cvParams.push_back(MS_LCQ_Deca);
    dummy.instrumentConfigurationPtrs.back()->userParams.push_back(UserParam("doobie", "420"));

    SpectrumListPtr sl = SpectrumList_MGF::create(is, dummy);

    if (os_)
    {
        TextWriter write(*os_);
        write(*sl);
        *os_ << endl;
    }

    // check easy functions

    unit_assert(sl.get());
    unit_assert(sl->size() == 3);
    unit_assert(sl->find("index=0") == 0);
    unit_assert(sl->find("index=1") == 1);
    unit_assert(sl->find("index=2") == 2);

    // find the second spectrum by TITLE field
    IndexList list = sl->findSpotID("small.pwiz.0004.0004.2");
    unit_assert(list.size() == 1);
    unit_assert(list[0] == 1);

    // look for a non-existent TITLE field
    list.clear();
    list = sl->findSpotID("fake title string");
    unit_assert(list.size() == 0);

    // check scan 0

    unit_assert(sl->spectrumIdentity(0).index == 0);
    unit_assert(sl->spectrumIdentity(0).id == "index=0");
    unit_assert(sl->spectrumIdentity(0).sourceFilePosition != -1);

    SpectrumPtr s = sl->spectrum(0, false);

    unit_assert(s.get());
    unit_assert(s->id == "index=0");
    unit_assert(s->index == 0);
    unit_assert(s->sourceFilePosition != -1);
    unit_assert(s->cvParam(MS_spectrum_title).value == "small.pwiz.0003.0003.2");
    unit_assert(s->cvParam(MS_ms_level).valueAs<int>() == 2);
    unit_assert_equal(s->cvParam(MS_total_ion_current).valueAs<double>(), 64.992226, 1e-5);
    unit_assert_equal(s->cvParam(MS_base_peak_m_z).valueAs<double>(), 231.38884, 1e-5);
    unit_assert_equal(s->cvParam(MS_base_peak_intensity).valueAs<double>(), 26.545113, 1e-5);

    unit_assert(s->precursors.size() == 1);
    Precursor& precursor0 = s->precursors[0];
    unit_assert(precursor0.selectedIons.size() == 1);
    unit_assert_equal(precursor0.selectedIons[0].cvParam(MS_selected_ion_m_z).valueAs<double>(), 810.79, 1e-5);

    unit_assert(s->defaultArrayLength == 3);
    unit_assert(s->binaryDataArrayPtrs.empty());

    s = sl->spectrum(0, true);
    unit_assert(s->defaultArrayLength == 3);
    unit_assert(s->binaryDataArrayPtrs.size() == 2);
    unit_assert(!s->binaryDataArrayPtrs[0]->data.empty() && !s->binaryDataArrayPtrs[1]->data.empty());

    vector<MZIntensityPair> pairs;
    s->getMZIntensityPairs(pairs);

    if (os_)
    {
        *os_ << "scan 0:\n";
        copy(pairs.begin(), pairs.end(), ostream_iterator<MZIntensityPair>(*os_, "\n"));
        *os_ << endl;
    }


    // check scan 1

    unit_assert(sl->spectrumIdentity(1).index == 1);
    unit_assert(sl->spectrumIdentity(1).id == "index=1");

    s = sl->spectrum(1, true);
    unit_assert(s.get());
    unit_assert(s->id == "index=1");
    unit_assert(s->index == 1);
    unit_assert(s->sourceFilePosition != -1);
    unit_assert(s->cvParam(MS_spectrum_title).value == "small.pwiz.0004.0004.2");
    unit_assert(s->cvParam(MS_ms_level).valueAs<int>() == 2);
    unit_assert(s->scanList.scans.size() == 1);
    unit_assert_equal(s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds(), 123.456, 1e-5);

    unit_assert(s->precursors.size() == 1);
    Precursor& precursor1 = s->precursors[0];
    unit_assert(precursor1.selectedIons.size() == 1);
    unit_assert_equal(precursor1.selectedIons[0].cvParam(MS_selected_ion_m_z).valueAs<double>(), 837.34, 1e-5);
    unit_assert(precursor1.selectedIons[0].cvParam(MS_charge_state).value == "2");

    unit_assert(s->defaultArrayLength == 5);

    pairs.clear();
    s->getMZIntensityPairs(pairs);

    unit_assert(s->defaultArrayLength == pairs.size());

    if (os_)
    {
        *os_ << "scan 1:\n";
        copy(pairs.begin(), pairs.end(), ostream_iterator<MZIntensityPair>(*os_, "\n"));
        *os_ << endl;
    }

    // check scan 2

    unit_assert(sl->spectrumIdentity(2).index == 2);
    unit_assert(sl->spectrumIdentity(2).id == "index=2");

    s = sl->spectrum(2, true);
    unit_assert(s.get());
    unit_assert(s->id == "index=2");
    unit_assert(s->index == 2);
    unit_assert(s->sourceFilePosition != -1);
    unit_assert(s->cvParam(MS_spectrum_title).value == "small.pwiz.0005.0005.2");
    unit_assert(s->cvParam(MS_ms_level).valueAs<int>() == 2);
    unit_assert(s->hasCVParam(MS_negative_scan));
    unit_assert(s->precursors.size() == 1);
    Precursor& precursor2 = s->precursors[0];
    unit_assert(precursor2.selectedIons.size() == 1);
    unit_assert_equal(precursor2.selectedIons[0].cvParam(MS_selected_ion_m_z).valueAs<double>(), 123.45, 1e-5);
    unit_assert_operator_equal("2", precursor2.selectedIons[0].cvParamChildren(MS_possible_charge_state)[0].value);
    unit_assert_operator_equal("3", precursor2.selectedIons[0].cvParamChildren(MS_possible_charge_state)[1].value);
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


