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


//#include "MSData.hpp"
#include "../common/unit.hpp"
#include <stdexcept>


using namespace System;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::data;
using namespace pwiz::CLI::msdata;
using namespace pwiz::CLI::util;

#using<System.Core.dll>

void testSpectrumListSimple()
{
    // fill in SpectrumListSimple

    SpectrumListSimple^ spectrumListSimple = gcnew SpectrumListSimple();

    Spectrum^ spectrum0 = gcnew Spectrum();
    spectrum0->index = 0;
    spectrum0->id = "sample=1 period=1 cycle=123 experiment=2";

    // add m/z values 0,...,9
    BinaryDataArray^ bd_mz = gcnew BinaryDataArray();
    for (unsigned int i=0; i<10; i++) bd_mz->data->Add(i);
    bd_mz->set(CVID::MS_m_z_array);
    //double* buffer = &bd_mz->data->Item(0);

    // add intensity values 10,...,1 
    BinaryDataArray^ bd_intensity = gcnew BinaryDataArray();
    for (unsigned int i=0; i<10; i++) bd_intensity->data->Add(10-i);
    bd_intensity->set(CVID::MS_intensity_array);

    spectrum0->binaryDataArrays->Add(bd_mz);
    spectrum0->binaryDataArrays->Add(bd_intensity);
    spectrum0->defaultArrayLength = 10;
    
    Spectrum^ spectrum1 = gcnew Spectrum();
    spectrum1->index = 1;
    spectrum1->id = "sample=1 period=1 cycle=345 experiment=2";
    spectrum1->set(CVID::MS_MSn_spectrum);
    spectrum1->set(CVID::MS_ionization_type, 420);

    spectrumListSimple->spectra->Add(spectrum0);
    spectrumListSimple->spectra->Add(spectrum1);

    // let an MSData object hold onto it as a SpectrumListPtr

    MSData data;
    data.run->spectrumList = spectrumListSimple;

    // test SpectrumList interface

    // verify index()
    SpectrumList^ spectrumList = data.run->spectrumList;
    unit_assert(spectrumList->size() == 2);
    unit_assert(spectrumList->find("sample=1 period=1 cycle=123 experiment=2") == 0);
    unit_assert(spectrumList->find("sample=1 period=1 cycle=345 experiment=2") == 1);

    // verify findAbbreviated()
    unit_assert_operator_equal(0, spectrumList->findAbbreviated("1.1.123.2"));
    unit_assert_operator_equal(1, spectrumList->findAbbreviated("1.1.345.2"));

    // verify spectrumIdentity()

    SpectrumIdentity^ identity0 = spectrumList->spectrumIdentity(0);
    unit_assert(identity0->index == spectrum0->index);
    unit_assert(identity0->id == spectrum0->id);

    SpectrumIdentity^ identity1 = spectrumList->spectrumIdentity(1);
    unit_assert(identity1->index == spectrum1->index);
    unit_assert(identity1->id == spectrum1->id);

    // verify spectrum 0
    Spectrum^ spectrum = spectrumList->spectrum(0);
    unit_assert(spectrum->index == spectrum0->index);
    unit_assert(spectrum->id == spectrum0->id);
    
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
}


void testChromatograms()
{
    ChromatogramListSimple^ cls = gcnew ChromatogramListSimple();

    for (int i=0; i<3; i++)
    {
        TimeIntensityPairList^ pairs = gcnew TimeIntensityPairList();
        for (int j=0; j<10; j++) pairs->Add(gcnew TimeIntensityPair(j, 10*i+j));
        cls->chromatograms->Add(gcnew Chromatogram());
        cls->chromatograms->default[cls->chromatograms->Count-1]->setTimeIntensityPairs(pairs, CVID::UO_minute, CVID::MS_number_of_detector_counts);
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


void testExample()
{
    MSData tiny;
    examples::initializeTiny(%tiny);

    SpectrumList^ sl = tiny.run->spectrumList;
    Spectrum^ s = sl->spectrum(1, false);
    unit_assert(s->precursors->Count == 1);
    Precursor^ p = s->precursors[0];
    IsolationWindow^ iw = p->isolationWindow;
    unit_assert_equal(s->precursors[0]->isolationWindow->cvParam(CVID::MS_isolation_window_lower_offset)->value, 0.5, 1e-6);

    auto mzArray = s->getMZArray()->data;
    unit_assert_operator_equal(10, mzArray->Storage()->Length);
}


void testCatchAndForward()
{
    // cause some exceptions in native code and test that we can catch them as .NET exceptions
    SpectrumListSimple^ sl = gcnew SpectrumListSimple();
    unit_assert_throws_what(sl->spectrum(123), System::Exception, "[MSData::SpectrumListSimple::spectrum()] Invalid index.");

    // TODO: where should we test catch-and-forward for other unbound classes, e.g. Reader_Thermo?
}


void testReader()
{
    auto typeSet = gcnew System::Collections::Generic::HashSet<String^>(ReaderList::FullReaderList->getTypes());
    unit_assert(typeSet->Contains("Sciex WIFF/WIFF2"));
    unit_assert(typeSet->Contains("mzML"));

    auto extSet = gcnew System::Collections::Generic::HashSet<String^>(ReaderList::FullReaderList->getFileExtensions());
    unit_assert(extSet->Contains(".wiff"));
    unit_assert(extSet->Contains(".wiff2"));
    unit_assert(extSet->Contains(".mzml"));

    auto typeExtMap = ReaderList::FullReaderList->getFileExtensionsByType();
    unit_assert(typeExtMap->ContainsKey("Sciex WIFF/WIFF2") && typeExtMap["Sciex WIFF/WIFF2"]->Count == 2 && typeExtMap["Sciex WIFF/WIFF2"][0] == ".wiff" && typeExtMap["Sciex WIFF/WIFF2"][1] == ".wiff2");
    unit_assert(typeExtMap->ContainsKey("Waters UNIFI") && typeExtMap["Waters UNIFI"]->Count == 0);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_CLI")

    try
    {
        testSpectrumListSimple();
        testChromatograms();
        testExample();
        testCatchAndForward();
        testReader();
    }
    catch (exception& e)
    {
        TEST_FAILED("std::exception: " + string(e.what()))
    }
    catch (System::Exception^ e)
    {
        TEST_FAILED("System.Exception: " + ToStdString(e->Message))
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
