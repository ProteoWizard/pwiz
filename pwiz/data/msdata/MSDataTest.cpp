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

    SpectrumPtr spectrum0(new Spectrum);
    spectrum0->index = 0;
    spectrum0->id = "id1";
    spectrum0->nativeID = "420";

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
    spectrum1->id = "id2";
    spectrum1->nativeID = "666";
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
    unit_assert(spectrumList.find("id1") == 0);
    unit_assert(spectrumList.find("id2") == 1);
    unit_assert(spectrumList.findNative("420") == 0);
    unit_assert(spectrumList.findNative("666") == 1);

    // verify spectrumIdentity()

    const SpectrumIdentity& identity0 = spectrumList.spectrumIdentity(0);
    unit_assert(identity0.index == spectrum0->index);
    unit_assert(identity0.id == spectrum0->id);
    unit_assert(identity0.nativeID == spectrum0->nativeID);

    const SpectrumIdentity& identity1 = spectrumList.spectrumIdentity(1);
    unit_assert(identity1.index == spectrum1->index);
    unit_assert(identity1.id == spectrum1->id);
    unit_assert(identity1.nativeID == spectrum1->nativeID);

    // verify spectrum 0
    SpectrumPtr spectrum = spectrumList.spectrum(0);
    unit_assert(spectrum->index == spectrum0->index);
    unit_assert(spectrum->id == spectrum0->id);
    unit_assert(spectrum->nativeID == spectrum0->nativeID);
    
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
    unit_assert(spectrum->nativeID == spectrum1->nativeID);
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
}


int main()
{
    try
    {
        testParamContainer();
        testSpectrumListSimple();
        testChromatograms();
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


