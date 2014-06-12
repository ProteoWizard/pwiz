//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#include "MSData.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::data;
using namespace pwiz::msdata;


void testSpectrumListSimple()
{
    // fill in SpectrumListSimple

    shared_ptr<SpectrumListSimple> spectrumListSimple(new SpectrumListSimple);

    unit_assert(spectrumListSimple->empty());
    spectrumListSimple->dp = DataProcessingPtr(new DataProcessing("dp"));
    unit_assert(!spectrumListSimple->empty());

    SpectrumPtr spectrum0(new Spectrum);
    spectrum0->index = 0;
    spectrum0->id = "sample=1 period=1 cycle=123 experiment=2";

    // add m/z values 0,...,9
    BinaryDataArrayPtr bd_mz(new BinaryDataArray);
    for (unsigned int i=0; i<10; i++) bd_mz->data.push_back(i);
    bd_mz->cvParams.push_back(MS_m_z_array);
    double* buffer = &bd_mz->data[0];

    // add intensity values 10,...,1 
    BinaryDataArrayPtr bd_intensity(new BinaryDataArray);
    for (unsigned int i=0; i<10; i++) bd_intensity->data.push_back(10-i);
    bd_intensity->cvParams.push_back(MS_intensity_array);

    spectrum0->binaryDataArrayPtrs.push_back(bd_mz);
    spectrum0->binaryDataArrayPtrs.push_back(bd_intensity);
    spectrum0->defaultArrayLength = 10;
    
    SpectrumPtr spectrum1(new Spectrum);
    spectrum1->index = 1;
    spectrum1->id = "sample=1 period=1 cycle=345 experiment=2";
    spectrum1->cvParams.push_back(MS_MSn_spectrum);
    spectrum1->cvParams.push_back(CVParam(MS_ionization_type, 420));

    spectrumListSimple->spectra.push_back(spectrum0);
    spectrumListSimple->spectra.push_back(spectrum1);

    // let an MSData object hold onto it as a SpectrumListPtr

    MSData data;
    data.run.spectrumListPtr = spectrumListSimple;

    // test SpectrumList interface

    // verify index()
    const SpectrumList& spectrumList = *data.run.spectrumListPtr;
    unit_assert_operator_equal(2, spectrumList.size());
    unit_assert_operator_equal(0, spectrumList.find("sample=1 period=1 cycle=123 experiment=2"));
    unit_assert_operator_equal(1, spectrumList.find("sample=1 period=1 cycle=345 experiment=2"));

    // verify findAbbreviated()
    unit_assert_operator_equal(0, spectrumList.findAbbreviated("1.1.123.2"));
    unit_assert_operator_equal(1, spectrumList.findAbbreviated("1.1.345.2"));

    // verify findNameValue

    IndexList result = spectrumList.findNameValue("cycle", "123");
    unit_assert(result.size()==1 && result[0]==0);

    result = spectrumList.findNameValue("cycle", "345");
    unit_assert(result.size()==1 && result[0]==1);

    // verify spectrumIdentity()

    const SpectrumIdentity& identity0 = spectrumList.spectrumIdentity(0);
    unit_assert(identity0.index == spectrum0->index);
    unit_assert(identity0.id == spectrum0->id);

    const SpectrumIdentity& identity1 = spectrumList.spectrumIdentity(1);
    unit_assert(identity1.index == spectrum1->index);
    unit_assert(identity1.id == spectrum1->id);

    // verify spectrum 0
    SpectrumPtr spectrum = spectrumList.spectrum(0);
    unit_assert(spectrum->index == spectrum0->index);
    unit_assert(spectrum->id == spectrum0->id);
    
    // verify no extra copying of binary data arrays
    unit_assert(spectrum->binaryDataArrayPtrs.size() == 2);
    unit_assert(&(spectrum->binaryDataArrayPtrs[0]->data[0]) == buffer);

    // verify getMZIntensityPairs()

    unit_assert(spectrum->binaryDataArrayPtrs[0]->hasCVParam(MS_m_z_array) == true);
    unit_assert(spectrum->binaryDataArrayPtrs[1]->hasCVParam(MS_intensity_array) == true);

    vector<MZIntensityPair> mziPairs;
    spectrum->getMZIntensityPairs(mziPairs);
    unit_assert(mziPairs.size() == 10);

    vector<double> doubleArray;
    unit_assert(spectrum->defaultArrayLength == 10);
    doubleArray.resize(spectrum->defaultArrayLength*2);
    spectrum->getMZIntensityPairs(reinterpret_cast<MZIntensityPair*>(&doubleArray[0]), 
                                  spectrum->defaultArrayLength);

    for (unsigned int i=0; i<10; i++)
    {
        const MZIntensityPair& p = mziPairs[i];
        unit_assert(p.mz == i);
        unit_assert(p.intensity == 10-i);
        unit_assert(doubleArray[2*i] == i);
        unit_assert(doubleArray[2*i+1] == 10-i);
    }

    // verify setMZIntensityPairs()
    spectrum->binaryDataArrayPtrs.clear();
    unit_assert(spectrum->binaryDataArrayPtrs.empty());
    vector<MZIntensityPair> mziPairs2;
    for (unsigned int i=0; i<10; i++)
        mziPairs2.push_back(MZIntensityPair(2*i, 3*i)); 
    spectrum->setMZIntensityPairs(mziPairs2, MS_number_of_detector_counts);
    unit_assert(spectrum->binaryDataArrayPtrs.size() == 2);
    unit_assert(spectrum->binaryDataArrayPtrs[0]->hasCVParam(MS_m_z_array) == true);
    unit_assert(spectrum->binaryDataArrayPtrs[1]->hasCVParam(MS_intensity_array) == true);
    unit_assert(spectrum->binaryDataArrayPtrs[1]->cvParam(MS_intensity_array).units == MS_number_of_detector_counts);
    unit_assert(spectrum->binaryDataArrayPtrs[0]->data.size() == 10);
    unit_assert(spectrum->binaryDataArrayPtrs[1]->data.size() == 10);
    for (unsigned int i=0; i<10; i++)
        unit_assert(spectrum->binaryDataArrayPtrs[0]->data[i] == 2*i &&
                    spectrum->binaryDataArrayPtrs[1]->data[i] == 3*i);

    // verify spectrum 1
    spectrum = spectrumList.spectrum(1);
    unit_assert(spectrum->index == spectrum1->index);
    unit_assert(spectrum->id == spectrum1->id);

    // verify DataProcessingPtr

    unit_assert(spectrumList.dataProcessingPtr().get() &&
                spectrumList.dataProcessingPtr()->id == "dp");
}


void testChromatograms()
{
    ChromatogramListSimple cls;

    for (int i=0; i<3; i++)
    {
        vector<TimeIntensityPair> pairs;
        for (int j=0; j<10; j++) pairs.push_back(TimeIntensityPair(j, 10*i+j));
        cls.chromatograms.push_back(ChromatogramPtr(new Chromatogram));
        cls.chromatograms.back()->setTimeIntensityPairs(pairs, UO_second, MS_number_of_detector_counts);
    }

    DataProcessingPtr dp(new DataProcessing("dp"));
    cls.dp = dp;

    ChromatogramList& cl = cls;

    unit_assert(cl.size() == 3);

    for (size_t i=0; i<3; i++)
    {
        vector<TimeIntensityPair> result; 
        cl.chromatogram(i)->getTimeIntensityPairs(result);
        unit_assert(result.size() == 10);
        for (size_t j=0; j<10; j++) 
            unit_assert(result[j].time==j  && result[j].intensity==10*i+j);
    }

    unit_assert(cl.dataProcessingPtr().get() &&
                cl.dataProcessingPtr()->id == "dp");
}


void testIDParsing()
{
    string id = "hair=blue favorite=420 age=36.175 upsideDown=1";

    map<string,string> parsedID = id::parse(id); 
    unit_assert(parsedID.size() == 4);

    unit_assert(id::value(id, "hair") == "blue");
    unit_assert(id::valueAs<int>(id, "favorite") == 420);
    unit_assert_equal(id::valueAs<double>(id, "age"), 36.175, 1e-6);
    unit_assert(id::valueAs<bool>(id, "upsideDown") == true);

    unit_assert(id::abbreviate(id) == "blue.420.36.175.1");
    unit_assert(id::abbreviate(id, ',') == "blue,420,36.175,1");

    id = "controllerType=0 controllerNumber=1 scan=123";
    unit_assert(id::translateNativeIDToScanNumber(MS_Thermo_nativeID_format, id) == "123");
    unit_assert(id::translateNativeIDToScanNumber(MS_no_nativeID_format, id) == "");
    unit_assert(id::translateNativeIDToScanNumber(MS_scan_number_only_nativeID_format, id) == "123");
    unit_assert(id::translateScanNumberToNativeID(MS_Thermo_nativeID_format, "123") == id);
    unit_assert(id::abbreviate(id) == "0.1.123");
    unit_assert(id::abbreviate(id, ',') == "0,1,123");

    unit_assert(id::translateScanNumberToNativeID(MS_multiple_peak_list_nativeID_format, "123") == "index=123");

    id = "spectrum=123";
    unit_assert(id::translateNativeIDToScanNumber(MS_spectrum_identifier_nativeID_format, id) == "123");
    unit_assert(id::translateScanNumberToNativeID(MS_spectrum_identifier_nativeID_format, "123") == id);
    unit_assert(id::translateNativeIDToScanNumber(MS_Thermo_nativeID_format, id) == "");

    id = "scan=123";
    unit_assert(id::translateNativeIDToScanNumber(MS_scan_number_only_nativeID_format, id) == "123");
    unit_assert(id::translateNativeIDToScanNumber(CVID_Unknown, id) == "123");
    unit_assert(id::translateScanNumberToNativeID(MS_scan_number_only_nativeID_format, "123") == id);
    unit_assert(id::translateScanNumberToNativeID(MS_Bruker_Agilent_YEP_nativeID_format, "123") == id);
    unit_assert(id::translateScanNumberToNativeID(MS_Bruker_BAF_nativeID_format, "123") == id);
    unit_assert(id::translateNativeIDToScanNumber(MS_Thermo_nativeID_format, id) == "");
    unit_assert(id::translateNativeIDToScanNumber(MS_spectrum_identifier_nativeID_format, id) == "");
    unit_assert(id::translateNativeIDToScanNumber(MS_multiple_peak_list_nativeID_format, id) == "");
    unit_assert(id::abbreviate(id) == "123");
    unit_assert(id::abbreviate(id, ',') == "123");

    id = "sample=1 period=2 cycle=123 experiment=3";
    unit_assert(id::translateNativeIDToScanNumber(MS_WIFF_nativeID_format, id) == "");
    unit_assert(id::translateScanNumberToNativeID(MS_WIFF_nativeID_format, "123") == "");
    unit_assert(id::abbreviate(id) == "1.2.123.3");
    unit_assert(id::abbreviate(id, ',') == "1,2,123,3");
}


void testAllDataProcessing()
{
    MSData msd;
    SpectrumListSimplePtr sl(new SpectrumListSimple);
    msd.run.spectrumListPtr = sl;

    DataProcessingPtr realDeal(new DataProcessing("dp"));
    DataProcessingPtr poser(new DataProcessing("dp"));

    msd.dataProcessingPtrs.push_back(realDeal);
    sl->dp = poser;

    // test allDataProcessingPtrs()
    vector<DataProcessingPtr> all = msd.allDataProcessingPtrs();
    unit_assert_operator_equal(1, all.size());
    unit_assert_operator_equal(realDeal, all[0]);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        testSpectrumListSimple();
        testChromatograms();
        testIDParsing();
        testAllDataProcessing();
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


