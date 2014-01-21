//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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

//#include "Diff.hpp"
//#include "examples.hpp"
#include "../common/unit.hpp"


using namespace pwiz::CLI::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::data;
using namespace pwiz::CLI::msdata;
using System::String;
using System::Console;


public ref class Log
{
    public: static System::IO::TextWriter^ writer = nullptr;
};


void testMSData()
{
    if (Log::writer != nullptr) Log::writer->Write("testMSData()\n");

    MSData a, b;
   
    a.id = "goober";
    b.id = "goober";

    Diff diff(a, b);
    unit_assert(!(bool)diff);

    a.accession = "different";
    a.cvs->Add(gcnew CV());
    b.fileDescription->fileContent->set(CVID::MS_reflectron_on);
    a.paramGroups->Add(gcnew ParamGroup("pg"));
    b.samples->Add(gcnew Sample("sample"));
    a.instrumentConfigurationList->Add(gcnew InstrumentConfiguration("instrumentConfiguration"));
    b.softwareList->Add(gcnew Software("software"));
    a.dataProcessingList->Add(gcnew DataProcessing("dataProcessing"));
    b.run->id = "run";
    b.scanSettingsList->Add(gcnew ScanSettings("scanSettings"));
   
    diff.apply(a, b);
    if (Log::writer != nullptr) Log::writer->WriteLine((String^)diff);
    unit_assert((bool)diff);

    unit_assert(diff.a_b->accession == "different");
    unit_assert(diff.b_a->accession->Length == 0);

    unit_assert(diff.a_b->cvs->Count == 1);
    unit_assert(diff.b_a->cvs->Count == 0);

    unit_assert(diff.a_b->fileDescription->empty());
    unit_assert(!diff.b_a->fileDescription->empty());

    unit_assert(!diff.a_b->paramGroups->Count == 0);
    unit_assert(diff.b_a->paramGroups->Count == 0);

    unit_assert(diff.a_b->samples->Count == 0);
    unit_assert(!diff.b_a->samples->Count == 0);

    unit_assert(!diff.a_b->instrumentConfigurationList->Count == 0);
    unit_assert(diff.b_a->instrumentConfigurationList->Count == 0);

    unit_assert(diff.a_b->softwareList->Count == 0);
    unit_assert(!diff.b_a->softwareList->Count == 0);

    unit_assert(!diff.a_b->dataProcessingList->Count == 0);
    unit_assert(diff.b_a->dataProcessingList->Count == 0);

    unit_assert(diff.a_b->run->empty());
    unit_assert(!diff.b_a->run->empty());

    unit_assert(diff.a_b->scanSettingsList->Count == 0);
    unit_assert(!diff.b_a->scanSettingsList->Count == 0);
}


/*void testMSData_allDataProcessingPtrs()
{
    if (Log::writer != nullptr) Log::writer->Write("testMSData_allDataProcessingPtrs()\n");

    MSData a, b;
   
    a.id = "goober";
    b.id = "goober";

    SpectrumListSimplePtr sl1(new SpectrumListSimple), sl2(new SpectrumListSimple);

    sl1->dp = gcnew DataProcessing("dp"));
    b.dataProcessingPtrs.push_back(gcnew DataProcessing("dp"));

    a.run.spectrumListPtr = sl1;
    b.run.spectrumListPtr = sl2;

    Diff diff(a, b);
    if (os_ && (bool)diff) *os_ << diff << endl;
    unit_assert(!(bool)diff);

    a.dataProcessingPtrs.push_back(gcnew DataProcessing("dp"));

    diff(a, b);
    unit_assert((bool)diff);
    unit_assert(diff.a_b->dataProcessingPtrs.size() == 1 &&
                diff.a_b->dataProcessingPtrs[0]->id == "more_dp");
}*/


/*void testBinaryDataOnly()
{
    MSData tiny;
    examples::initializeTiny(tiny);

    MSData tinier;
    SpectrumListSimple^ sl = gcnew SpectrumListSimple();
    ChromatogramListSimple^ cl = gcnew ChromatogramListSimple();
    tinier.run->spectrumList = sl; 
    tinier.run->chromatogramList = cl; 

    for (unsigned int i=0; i<tiny.run.spectrumList->size(); i++)
    {
        SpectrumPtr from = tiny.run.spectrumList->spectrum(i, true);
        sl->spectra.push_back(SpectrumPtr(new Spectrum));
        SpectrumPtr& to = sl->spectra.back();   

        for (vector<BinaryDataArrayPtr>::const_iterator it=from->binaryDataArrayPtrs.begin();
             it!=from->binaryDataArrayPtrs.end(); ++it)
        {
            // copy BinaryDataArray::data from tiny to tinier
            to->binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
            to->binaryDataArrayPtrs.back()->data = (*it)->data;
        }

        // copy "important" scan metadata

        to->index = from->index;
        to->defaultArrayLength = from->defaultArrayLength;
        to->scanList = from->scanList;

        to->precursors.resize(from->precursors.size());
        for (size_t precursorIndex=0; precursorIndex<from->precursors.size(); ++precursorIndex)
        {
            Precursor& precursorTo = to->precursors[precursorIndex];
            Precursor& precursorFrom = from->precursors[precursorIndex];
            precursorTo.selectedIons = precursorFrom.selectedIons;
        }
    }

    for (unsigned int i=0; i<tiny.run.chromatogramListPtr->size(); i++)
    {
        ChromatogramPtr from = tiny.run.chromatogramListPtr->chromatogram(i, true);
        cl->chromatograms.push_back(ChromatogramPtr(new Chromatogram));
        ChromatogramPtr& to = cl->chromatograms.back();   

        for (vector<BinaryDataArrayPtr>::const_iterator it=from->binaryDataArrayPtrs.begin();
             it!=from->binaryDataArrayPtrs.end(); ++it)
        {
            // copy BinaryDataArray::data from tiny to tinier
            to->binaryDataArrayPtrs.push_back(BinaryDataArrayPtr(new BinaryDataArray));
            to->binaryDataArrayPtrs.back()->data = (*it)->data;
        }

        // copy "important" scan metadata

        to->index = from->index;
        to->defaultArrayLength = from->defaultArrayLength;
    }

    if (os_)
    {
        *os_ << "tinier::";
        TextWriter(*os_,0)(tinier);
    }

    Diff diff_full(tiny, tinier);
    unit_assert(diff_full);

    DiffConfig config;
    config.ignoreMetadata = true;

    Diff diff_data(tiny, tinier, config);
    if (os_ && diff_data) *os_ << diff_data << endl;
    unit_assert(!diff_data); 
}*/


void test()
{
    testMSData();
    //testMSData_allDataProcessingPtrs();
    //testBinaryDataOnly();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_MSData_CLI")

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) Log::writer = Console::Out;
        test();
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

