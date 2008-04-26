//
// SpectrumList_mzXML_Test.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#include "SpectrumList_mzXML.hpp"
#include "Serializer_mzXML.hpp" // depends on Serializer_mzXML::write() only
#include "TextWriter.hpp"
#include "examples.hpp"
#include "utility/minimxml/XMLWriter.hpp"
#include "utility/misc/unit.hpp"
#include <iostream>
#include <iterator>


using namespace std;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;
using boost::shared_ptr;


ostream* os_ = 0;


void test(bool indexed)
{
    if (os_) *os_ << "test(): indexed=\"" << boolalpha << indexed << "\"\n";

    MSData tiny;
    examples::initializeTiny(tiny);

    Serializer_mzXML::Config config;
    config.indexed = indexed;
    Serializer_mzXML serializer(config);

    ostringstream oss;
    serializer.write(oss, tiny);

    if (os_) *os_ << "oss:\n" << oss.str() << endl;

    shared_ptr<istream> is(new istringstream(oss.str()));

    // dummy would normally be read in from file
  
    MSData dummy;
    dummy.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration("LCQDeca")));
    dummy.instrumentConfigurationPtrs.back()->cvParams.push_back(MS_LCQ_Deca);
    dummy.instrumentConfigurationPtrs.back()->userParams.push_back(UserParam("doobie", "420"));

    if (!indexed)
    {
        bool caught = false;

        try 
        {
            SpectrumListPtr temp = SpectrumList_mzXML::create(is, dummy, true);
        }
        catch (SpectrumList_mzXML::index_not_found&)
        {
            if (os_) *os_ << "Caught index_not_found: ok!\n";
            caught = true;
        }
        
        unit_assert(caught);
    }

    SpectrumListPtr sl = SpectrumList_mzXML::create(is, dummy, indexed);

    if (os_)
    {
        TextWriter write(*os_);
        write(*sl);
        *os_ << endl;
    }

    // check easy functions

    unit_assert(sl.get());
    unit_assert(sl->size() == 2);
    unit_assert(sl->find("19") == 0);
    unit_assert(sl->findNative("19") == 0);
    unit_assert(sl->find("20") == 1);
    unit_assert(sl->findNative("20") == 1);

    // check scan 19

    unit_assert(sl->spectrumIdentity(0).index == 0);
    unit_assert(sl->spectrumIdentity(0).id == "19");
    unit_assert(sl->spectrumIdentity(0).nativeID == "19");
    unit_assert(sl->spectrumIdentity(0).sourceFilePosition != -1);

    SpectrumPtr s = sl->spectrum(0, false);

    unit_assert(s.get());
    unit_assert(s->id == "19");
    unit_assert(s->index == 0);
    unit_assert(s->nativeID == "19");
    unit_assert(s->sourceFilePosition != -1);
    unit_assert(s->cvParam(MS_ms_level).valueAs<int>() == 1);
    unit_assert(s->spectrumDescription.scan.hasCVParam(MS_positive_scan));
    unit_assert(s->spectrumDescription.scan.hasCVParam(MS_scan_time));
    unit_assert(s->spectrumDescription.scan.cvParam(MS_preset_scan_configuration).valueAs<int>() == 3);
    unit_assert(s->spectrumDescription.cvParam(MS_base_peak_intensity).value == "120053");
    unit_assert(s->binaryDataArrayPtrs.empty());

    s = sl->spectrum(0, true);
    unit_assert(s->binaryDataArrayPtrs.size() == 2);

    vector<MZIntensityPair> pairs;
    s->getMZIntensityPairs(pairs);

    if (os_)
    {
        *os_ << "scan 19:\n";
        copy(pairs.begin(), pairs.end(), ostream_iterator<MZIntensityPair>(*os_, "\n"));
        *os_ << endl;
    }

    unit_assert(pairs.size() == 15);
    for (int i=0; i<15; i++)
        unit_assert(pairs[i].mz==i && pairs[i].intensity==15-i);

    unit_assert(s->spectrumDescription.scan.instrumentConfigurationPtr.get());
    InstrumentConfiguration& instrumentConfiguration = *s->spectrumDescription.scan.instrumentConfigurationPtr;
    unit_assert(!instrumentConfiguration.cvParams.empty()); // references resolved
    unit_assert(instrumentConfiguration.userParams.size() == 1 &&
                instrumentConfiguration.userParams[0].name == "doobie");

    // check scan 20

    unit_assert(sl->spectrumIdentity(1).index == 1);
    unit_assert(sl->spectrumIdentity(1).id == "20");
    unit_assert(sl->spectrumIdentity(1).nativeID == "20");

    s = sl->spectrum(1, true);
    unit_assert(s.get());
    unit_assert(s->id == "20");
    unit_assert(s->index == 1);
    unit_assert(s->nativeID == "20");
    unit_assert(s->sourceFilePosition != -1);
    unit_assert(s->cvParam(MS_ms_level).valueAs<int>() == 2);
    unit_assert(s->spectrumDescription.scan.cvParam(MS_preset_scan_configuration).valueAs<int>() == 4);

    unit_assert(s->spectrumDescription.precursors.size() == 1);
    Precursor& precursor = s->spectrumDescription.precursors[0];
    unit_assert(precursor.selectedIons.size() == 1);
    unit_assert(precursor.selectedIons[0].hasCVParam(MS_m_z));
    unit_assert(precursor.selectedIons[0].hasCVParam(MS_intensity));
    unit_assert(precursor.selectedIons[0].hasCVParam(MS_charge_state));
    unit_assert(precursor.activation.hasCVParam(MS_collision_energy));
    unit_assert(precursor.spectrumID == "19"); // Serializer_mzXML::read() sets id="19", not "S19"

    pairs.clear();
    s->getMZIntensityPairs(pairs);

    if (os_)
    {
        *os_ << "scan 20:\n";
        copy(pairs.begin(), pairs.end(), ostream_iterator<MZIntensityPair>(*os_, "\n"));
        *os_ << endl;
    }

    unit_assert(pairs.size() == 10);
    for (int i=0; i<10; i++)
        unit_assert(pairs[i].mz==2*i && pairs[i].intensity==(10-i)*2);
}


void test()
{
    bool indexed = true;
    test(indexed);

    indexed = false;
    test(indexed);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
}


