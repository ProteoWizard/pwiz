//
// MSDataTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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
#include "boost/lexical_cast.hpp"
#include <iostream>
#include <iterator>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::msdata;
using boost::shared_ptr;
using boost::lexical_cast;


void testParamContainer()
{
    ParamContainer pc;
    pc.cvParams.push_back(MS_reflectron_on);
    pc.cvParams.push_back(MS_MSn_spectrum);
    pc.cvParams.push_back(MS_reflectron_off);
    pc.cvParams.push_back(CVParam(MS_ionization_type, 420));
    pc.userParams.push_back(UserParam("name1", "1", "type1", UO_second));
    pc.userParams.push_back(UserParam("name2", "2", "type2", UO_minute));

    ParamGroupPtr pg(new ParamGroup);
    pg->cvParams.push_back(CVParam(UO_dalton, 666));
    pc.paramGroupPtrs.push_back(pg);
   
    unit_assert(pc.hasCVParam(MS_reflectron_off));
    unit_assert(!pc.hasCVParam(MS_spectrum_type));
    unit_assert(pc.hasCVParam(UO_dalton));
    unit_assert(!pc.hasCVParam(UO_mass_unit));
      
    unit_assert(pc.hasCVParamChild(MS_spectrum_type));
    unit_assert(pc.hasCVParamChild(UO_mass_unit));

    unit_assert(pc.cvParam(MS_m_z) == CVID_Unknown);
    unit_assert(pc.cvParam(MS_reflectron_off) == MS_reflectron_off);
    unit_assert(pc.cvParam(UO_mass_unit) == CVID_Unknown);
    unit_assert(pc.cvParam(UO_dalton).cvid == UO_dalton);

    unit_assert(pc.cvParamChild(MS_spectrum_type) == MS_MSn_spectrum);
    unit_assert(pc.cvParamChild(UO_mass_unit).cvid == UO_dalton);

    string result = "goober";
    result = pc.cvParam(MS_m_z).value;
    unit_assert(result == "");
    result = pc.cvParam(MS_ionization_type).value;
    unit_assert(result == "420");
    result = pc.cvParam(UO_dalton).value;
    unit_assert(result == "666");

    UserParam userParam = pc.userParam("name");
    unit_assert(userParam.empty());
    userParam = pc.userParam("name1");
    unit_assert(userParam.name == "name1");
    unit_assert(userParam.valueAs<int>() == 1);
    unit_assert(userParam.type == "type1");
    unit_assert(userParam.units == UO_second);
    userParam = pc.userParam("name2");
    unit_assert(userParam.name == "name2");
    unit_assert(userParam.valueAs<double>() == 2);
    unit_assert(userParam.type == "type2");
    unit_assert(userParam.units == UO_minute);
    unit_assert(pc.userParam("goober").valueAs<int>() == 0);

    pc.set(MS_ms_level, 2);
    unit_assert(pc.cvParam(MS_ms_level).valueAs<int>() == 2);
    pc.set(MS_ms_level, 3);
    unit_assert(pc.cvParam(MS_ms_level).valueAs<int>() == 3);

    pc.set(MS_deisotoping, true);
    unit_assert(pc.cvParam(MS_deisotoping).valueAs<bool>() == true);
    pc.set(MS_deisotoping, false);
    unit_assert(pc.cvParam(MS_deisotoping).valueAs<bool>() == false);
}


void testSpectrumListSimple()
{
    // fill in SpectrumListSimple

    shared_ptr<SpectrumListSimple> spectrumListSimple(new SpectrumListSimple);

    unit_assert(spectrumListSimple->empty());
    spectrumListSimple->dp = DataProcessingPtr(new DataProcessing("dp"));
    unit_assert(!spectrumListSimple->empty());

    SpectrumPtr spectrum0(new Spectrum);
    spectrum0->index = 0;
    spectrum0->id = "scan=1";

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
    spectrum1->id = "scan=2";
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
    unit_assert(spectrumList.size() == 2);
    unit_assert(spectrumList.find("scan=1") == 0);
    unit_assert(spectrumList.find("scan=2") == 1);

    // verify findNameValue

    IndexList result = spectrumList.findNameValue("scan", "1");
    unit_assert(result.size()==1 && result[0]==0);

    result = spectrumList.findNameValue("scan", "2");
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
    spectrum->setMZIntensityPairs(mziPairs2);
    unit_assert(spectrum->binaryDataArrayPtrs.size() == 2);
    unit_assert(spectrum->binaryDataArrayPtrs[0]->hasCVParam(MS_m_z_array) == true);
    unit_assert(spectrum->binaryDataArrayPtrs[1]->hasCVParam(MS_intensity_array) == true);
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
        cls.chromatograms.back()->setTimeIntensityPairs(pairs);
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
}


void testCurrentDataProcessing()
{
    MSData msd;
    SpectrumListSimplePtr sl(new SpectrumListSimple);
    msd.run.spectrumListPtr = sl;

    unit_assert(!msd.currentDataProcessingPtr().get());
    
    sl->dp = DataProcessingPtr(new DataProcessing("dp"));
    unit_assert(msd.currentDataProcessingPtr().get() &&
                msd.currentDataProcessingPtr()->id == "dp");

    msd.dataProcessingPtrs.push_back(DataProcessingPtr(new DataProcessing("dp")));
    unit_assert(msd.currentDataProcessingPtr().get() &&
                msd.currentDataProcessingPtr()->id == "more_dp");
}


int main()
{
    try
    {
        testParamContainer();
        testSpectrumListSimple();
        testChromatograms();
        testIDParsing();
        testCurrentDataProcessing();
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
        return 1;
    }
}


