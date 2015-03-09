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


#include "SpectrumList_mzXML.hpp"
#include "Serializer_mzXML.hpp" // depends on Serializer_mzXML::write() only
#include "TextWriter.hpp"
#include "examples.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;


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
    dummy.fileDescription.sourceFilePtrs.push_back(SourceFilePtr(new SourceFile("tiny1.yep")));
    dummy.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_Agilent_YEP_format);
    dummy.fileDescription.sourceFilePtrs.back()->set(MS_Bruker_Agilent_YEP_nativeID_format);
    dummy.softwarePtrs.push_back(SoftwarePtr(new Software("pwiz")));
    dummy.softwarePtrs.back()->set(MS_ProteoWizard_software);
    dummy.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration("1")));
    dummy.instrumentConfigurationPtrs.back()->set(MS_LCQ_Deca);
    dummy.instrumentConfigurationPtrs.back()->userParams.push_back(UserParam("doobie", "420"));
    dummy.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("DP1")));
    dummy.dataProcessingPtrs.back()->processingMethods.push_back(ProcessingMethod());
    dummy.dataProcessingPtrs.back()->processingMethods.back().set(MS_Conversion_to_mzML);
    dummy.dataProcessingPtrs.back()->processingMethods.back().softwarePtr = dummy.softwarePtrs.back();

    // note: used to have a test here to check that an exception would be thrown on 
	// on an unindexed input file, but index is an optional element so the right thing to
	// do is just create it

    SpectrumListPtr sl = SpectrumList_mzXML::create(is, dummy, indexed);

    if (os_)
    {
        TextWriter write(*os_);
        write(*sl);
        *os_ << endl;
    }

    // check easy functions

    unit_assert(sl.get());
    unit_assert(sl->size() == 4);

    unit_assert(sl->find("scan=19") == 0);
    IndexList indexList = sl->findNameValue("scan", "19");
    unit_assert(indexList.size()==1 && indexList[0]==0);
    unit_assert(sl->find("scan=20") == 1);
    indexList = sl->findNameValue("scan", "20");
    unit_assert(indexList.size()==1 && indexList[0]==1);
    unit_assert(sl->find("scan=21") == 2);
    indexList = sl->findNameValue("scan", "21");
    unit_assert(indexList.size()==1 && indexList[0]==2);

    // check scan 19

    unit_assert(sl->spectrumIdentity(0).index == 0);
    unit_assert(sl->spectrumIdentity(0).id == "scan=19");
    unit_assert(sl->spectrumIdentity(0).sourceFilePosition != -1);

    SpectrumPtr s = sl->spectrum(0, false);

    unit_assert(s.get());
    unit_assert(s->id == "scan=19");
    unit_assert(s->index == 0);
    unit_assert(s->sourceFilePosition != -1);
    unit_assert(s->cvParam(MS_ms_level).valueAs<int>() == 1);
    unit_assert(s->hasCVParam(MS_positive_scan));
    unit_assert(s->scanList.scans.size() == 1);
    Scan& scan = s->scanList.scans[0];
    unit_assert(scan.hasCVParam(MS_scan_start_time));
    //unit_assert(scan.cvParam(MS_preset_scan_configuration).valueAs<int>() == 3);
    unit_assert(s->cvParam(MS_base_peak_intensity).value == "120053");
    unit_assert(s->defaultArrayLength == 15);
    unit_assert(s->binaryDataArrayPtrs.size() == 2);
    unit_assert(s->binaryDataArrayPtrs[0]->hasCVParam(MS_m_z_array));
    unit_assert(s->binaryDataArrayPtrs[1]->hasCVParam(MS_intensity_array));
    unit_assert(s->binaryDataArrayPtrs[0]->data.empty() && s->binaryDataArrayPtrs[1]->data.empty());

    s = sl->spectrum(0, true);
    unit_assert(s->defaultArrayLength == 15);
    unit_assert(s->binaryDataArrayPtrs.size() == 2);
    unit_assert(!s->binaryDataArrayPtrs[0]->data.empty() && !s->binaryDataArrayPtrs[1]->data.empty());

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

    Scan& scan19 = s->scanList.scans[0];
    unit_assert(scan19.instrumentConfigurationPtr.get());
    InstrumentConfiguration& instrumentConfiguration = *scan19.instrumentConfigurationPtr;
    unit_assert(!instrumentConfiguration.cvParams.empty()); // references resolved
    unit_assert(instrumentConfiguration.userParams.size() == 1 &&
                instrumentConfiguration.userParams[0].name == "doobie");

    // check scan 20

    unit_assert(sl->spectrumIdentity(1).index == 1);
    unit_assert(sl->spectrumIdentity(1).id == "scan=20");

    s = sl->spectrum(1, true);
    unit_assert(s.get());
    unit_assert(s->id == "scan=20");
    unit_assert(s->index == 1);
    unit_assert(s->sourceFilePosition != -1);
    unit_assert(s->cvParam(MS_ms_level).valueAs<int>() == 2);

    unit_assert(s->scanList.scans.size() == 1);
    //Scan& scan20 = s->scanList.scans[0];
    //unit_assert(scan20.cvParam(MS_preset_scan_configuration).valueAs<int>() == 4);

    unit_assert(s->precursors.size() == 1);
    Precursor& precursor = s->precursors[0];
    unit_assert(precursor.selectedIons.size() == 1);
    unit_assert(precursor.selectedIons[0].hasCVParam(MS_selected_ion_m_z));
    unit_assert(precursor.selectedIons[0].hasCVParam(MS_peak_intensity));
    unit_assert(precursor.selectedIons[0].hasCVParam(MS_charge_state));
    unit_assert(precursor.activation.hasCVParam(MS_CID));
    unit_assert(precursor.activation.hasCVParam(MS_collision_energy));
    unit_assert(precursor.spectrumID == "scan=19"); // Serializer_mzXML::read() sets

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

    
    // check scan 21 (for userParam <-> nameValue)

    unit_assert(sl->spectrumIdentity(2).index == 2);
    unit_assert(sl->spectrumIdentity(2).id == "scan=21");

    s = sl->spectrum(2, false);
    unit_assert(s.get());
    unit_assert(s->id == "scan=21");
    UserParam exampleUserParam = s->userParam("example");
    unit_assert(!exampleUserParam.empty());
    unit_assert(exampleUserParam.name == "example");
    unit_assert(exampleUserParam.value == "spectrum with no data");
    unit_assert(exampleUserParam.type == "xsd:string");

    // check scan 22 (for ETD precursor activation)

    unit_assert(sl->spectrumIdentity(3).index == 3);
    unit_assert(sl->spectrumIdentity(3).id == "scan=22");

    s = sl->spectrum(3, false);
    unit_assert(s.get());
    unit_assert(s->id == "scan=22");
    unit_assert(s->precursors.size() == 1);
    Precursor& precursor22 = s->precursors[0];
    unit_assert(precursor22.selectedIons.size() == 1);
    unit_assert(precursor22.selectedIons[0].hasCVParam(MS_selected_ion_m_z));
    unit_assert(precursor22.selectedIons[0].hasCVParam(MS_peak_intensity));
    unit_assert(precursor22.selectedIons[0].hasCVParam(MS_charge_state));
    unit_assert(precursor22.activation.hasCVParam(MS_ETD));
    unit_assert(precursor22.activation.hasCVParam(MS_CID));
    unit_assert(precursor22.activation.hasCVParam(MS_collision_energy));
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


