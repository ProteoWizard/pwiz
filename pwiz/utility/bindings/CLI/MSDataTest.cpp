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


//#include "MSData.hpp"
#include "utility/misc/unit.hpp"

#using <mscorlib.dll>

using namespace System;
using namespace pwiz::CLI::msdata;
using namespace pwiz::util;

void testParamContainer()
{
    ParamContainer^ pc = gcnew ParamContainer();
    pc->cvParams()->Add(gcnew CVParam(CVID::MS_reflectron_on));
    pc->cvParams()->Add(gcnew CVParam(CVID::MS_MSn_spectrum));
    pc->cvParams()->Add(gcnew CVParam(CVID::MS_reflectron_off));
    pc->cvParams()->Add(gcnew CVParam(CVID::MS_ionization_type, 420));
    pc->userParams()->Add(gcnew UserParam("name1", "1", "type1", CVID::MS_second));
    pc->userParams()->Add(gcnew UserParam("name2", "2", "type2", CVID::MS_minute));

    ParamGroup^ pg = gcnew ParamGroup();
    pg->cvParams()->Add(gcnew CVParam(CVID::MS_Dalton, 666));
    pc->paramGroups()->Add(pg);

    unit_assert(pc->hasCVParam(CVID::MS_reflectron_off));
    unit_assert(!pc->hasCVParam(CVID::MS_spectrum_type));
    unit_assert(pc->hasCVParam(CVID::MS_Dalton));
    unit_assert(!pc->hasCVParam(CVID::MS_mass_unit));

    unit_assert(pc->hasCVParamChild(CVID::MS_spectrum_type));
    unit_assert(pc->hasCVParamChild(CVID::MS_mass_unit));

    unit_assert(pc->cvParam(CVID::MS_m_z) == CVID::CVID_Unknown);
    unit_assert(pc->cvParam(CVID::MS_reflectron_off) == CVID::MS_reflectron_off);
    unit_assert(pc->cvParam(CVID::MS_mass_unit) == CVID::CVID_Unknown);
    unit_assert(pc->cvParam(CVID::MS_Dalton)->cvid == CVID::MS_Dalton);

    unit_assert(pc->cvParamChild(CVID::MS_spectrum_type) == CVID::MS_MSn_spectrum);
    unit_assert(pc->cvParamChild(CVID::MS_mass_unit)->cvid == CVID::MS_Dalton);

    String^ result = gcnew String("goober");
    result = pc->cvParam(CVID::MS_m_z)->value;
    unit_assert(result == "");
    result = pc->cvParam(CVID::MS_ionization_type)->value;
    unit_assert(result == "420");
    result = pc->cvParam(CVID::MS_Dalton)->value;
    unit_assert(result == "666");

    UserParam^ userParam = pc->userParam("name");
    unit_assert(userParam->empty());
    userParam = pc->userParam("name1");
    unit_assert(userParam->name == "name1");
    unit_assert(int(userParam->value) == 1);
    unit_assert(userParam->type == "type1");
    unit_assert(userParam->units == CVID::MS_second);
    userParam = pc->userParam("name2");
    unit_assert(userParam->name == "name2");
    unit_assert(double(userParam->value) == 2);
    unit_assert(userParam->type == "type2");
    unit_assert(userParam->units == CVID::MS_minute);
    unit_assert(pc->userParam("goober")->value == 0);

    pc->set(CVID::MS_ms_level, 2);
    unit_assert(int(pc->cvParam(CVID::MS_ms_level)->value) == 2);
    pc->set(CVID::MS_ms_level, 3);
    unit_assert(int(pc->cvParam(CVID::MS_ms_level)->value) == 3);

    pc->set(CVID::MS_deisotoping, true);
    unit_assert(bool(pc->cvParam(CVID::MS_deisotoping)->value) == true);
    pc->set(CVID::MS_deisotoping, false);
    unit_assert(bool(pc->cvParam(CVID::MS_deisotoping)->value) == false);
}


void testSpectrumListSimple()
{
    // fill in SpectrumListSimple

    SpectrumListSimple^ spectrumListSimple = gcnew SpectrumListSimple();

    Spectrum^ spectrum0 = gcnew Spectrum();
    spectrum0->index = 0;
    spectrum0->id = "id1";
    spectrum0->nativeID = "420";

    // add m/z values 0,...,9
    BinaryDataArray^ bd_mz = gcnew BinaryDataArray();
    for (unsigned int i=0; i<10; i++) bd_mz->data->Add(i);
    bd_mz->cvParams()->Add(gcnew CVParam(CVID::MS_m_z_array));
    //double* buffer = &bd_mz->data->Item(0);

    // add intensity values 10,...,1 
    BinaryDataArray^ bd_intensity = gcnew BinaryDataArray();
    for (unsigned int i=0; i<10; i++) bd_intensity->data->Add(10-i);
    bd_intensity->cvParams()->Add(gcnew CVParam(CVID::MS_intensity_array));

    spectrum0->binaryDataArrays->Add(bd_mz);
    spectrum0->binaryDataArrays->Add(bd_intensity);
    spectrum0->defaultArrayLength = 10;
    
    Spectrum^ spectrum1 = gcnew Spectrum();
    spectrum1->index = 1;
    spectrum1->id = "id2";
    spectrum1->nativeID = "666";
    spectrum1->cvParams()->Add(gcnew CVParam(CVID::MS_MSn_spectrum));
    spectrum1->cvParams()->Add(gcnew CVParam(CVID::MS_ionization_type, 420));

    spectrumListSimple->spectra->Add(spectrum0);
    spectrumListSimple->spectra->Add(spectrum1);

    // let an MSData object hold onto it as a SpectrumListPtr

    //MSData^ data = gcnew MSData();
    //data->run->spectrumList = spectrumListSimple;

    // test SpectrumList interface

    // verify index()
    SpectrumList^ spectrumList = spectrumListSimple;//data->run->spectrumList;
    unit_assert(spectrumList->size() == 2);
    unit_assert(spectrumList->find("id1") == 0);
    unit_assert(spectrumList->find("id2") == 1);
    unit_assert(spectrumList->findNative("420") == 0);
    unit_assert(spectrumList->findNative("666") == 1);

    // verify spectrumIdentity()

    SpectrumIdentity^ identity0 = spectrumList->spectrumIdentity(0);
    unit_assert(identity0->index == spectrum0->index);
    unit_assert(identity0->id == spectrum0->id);
    unit_assert(identity0->nativeID == spectrum0->nativeID);

    SpectrumIdentity^ identity1 = spectrumList->spectrumIdentity(1);
    unit_assert(identity1->index == spectrum1->index);
    unit_assert(identity1->id == spectrum1->id);
    unit_assert(identity1->nativeID == spectrum1->nativeID);

    // verify spectrum 0
    Spectrum^ spectrum = spectrumList->spectrum(0);
    unit_assert(spectrum->index == spectrum0->index);
    unit_assert(spectrum->id == spectrum0->id);
    unit_assert(spectrum->nativeID == spectrum0->nativeID);
    
    // verify no extra copying of binary data arrays
    unit_assert(spectrum->binaryDataArrays->Count == 2);
    //unit_assert(&(spectrum->binaryDataArrays[0]->data[0]) == buffer);

    // verify getMZIntensityPairs()

    BinaryDataArrayList^ binaryDataArrays = spectrum->binaryDataArrays;
    BinaryDataArray^ binaryDataArray = binaryDataArrays->default[0];
    unit_assert(binaryDataArray->hasCVParam(CVID::MS_m_z_array) == true);
    unit_assert(spectrum->binaryDataArrays->default[1]->hasCVParam(CVID::MS_intensity_array) == true);

    MZIntensityPairList^ mziPairs;
    spectrum->getMZIntensityPairs(mziPairs);
    unit_assert(mziPairs->Count == 10);

    for (unsigned int i=0; i<10; i++)
    {
        MZIntensityPair^ p = mziPairs->default[i];
        unit_assert(p->mz == i);
        unit_assert(p->intensity == 10-i);
    }

    // verify setMZIntensityPairs()
    spectrum->binaryDataArrays->Clear();
    unit_assert(spectrum->binaryDataArrays->Count == 0);
    MZIntensityPairList^ mziPairs2 = gcnew MZIntensityPairList();
    for (unsigned int i=0; i<10; i++)
        mziPairs2->Add(gcnew MZIntensityPair(2*i, 3*i)); 
    spectrum->setMZIntensityPairs(mziPairs2);
    unit_assert(spectrum->binaryDataArrays->Count == 2);
    unit_assert(spectrum->binaryDataArrays->default[0]->hasCVParam(CVID::MS_m_z_array) == true);
    unit_assert(spectrum->binaryDataArrays->default[1]->hasCVParam(CVID::MS_intensity_array) == true);
    unit_assert(spectrum->binaryDataArrays->default[0]->data->Count == 10);
    unit_assert(spectrum->binaryDataArrays->default[1]->data->Count == 10);
    for (unsigned int i=0; i<10; i++)
        unit_assert(spectrum->binaryDataArrays->default[0]->data->default[i] == 2*i &&
                    spectrum->binaryDataArrays->default[1]->data->default[i] == 3*i);

    // verify spectrum 1
    spectrum = spectrumList->spectrum(1);
    unit_assert(spectrum->index == spectrum1->index);
    unit_assert(spectrum->id == spectrum1->id);
    unit_assert(spectrum->nativeID == spectrum1->nativeID);
}


void testChromatograms()
{
    ChromatogramListSimple^ cls = gcnew ChromatogramListSimple();

    for (int i=0; i<3; i++)
    {
        TimeIntensityPairList^ pairs = gcnew TimeIntensityPairList();
        for (int j=0; j<10; j++) pairs->Add(gcnew TimeIntensityPair(j, 10*i+j));
        cls->chromatograms->Add(gcnew Chromatogram());
        cls->chromatograms->default[cls->chromatograms->Count-1]->setTimeIntensityPairs(pairs);
    }

    ChromatogramList^ cl = cls;

    unit_assert(cl->size() == 3);

    for (int i=0; i<3; i++)
    {
        TimeIntensityPairList^ result;
        cl->chromatogram(i)->getTimeIntensityPairs(result);
        unit_assert(result->Count == 10);
        for (int j=0; j<10; j++) 
            unit_assert(result->default[j]->time==j && result->default[j]->intensity==10*i+j);
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
    catch (std::exception& e)
    {
        System::Console::Error->WriteLine(gcnew String(e.what()));
        return 1;
    }
    catch (System::Exception^ e)
    {
        System::Console::Error->WriteLine(e->Message);
        return 1;
    }
    catch (...)
    {
        System::Console::Error->WriteLine("Caught unknown exception.");
        return 1;
    }
}
