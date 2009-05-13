//
// MSDataFileTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


//#include "MSDataFile.hpp"
//#include "../../../data/msdata/Diff.hpp"
//#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"

#using <mscorlib.dll>

using namespace System;
using namespace pwiz::CLI::msdata;
using namespace pwiz::CLI::util;


public ref class test
{
public:
    bool accept(Spectrum^ s)
    {
        return (int) s->cvParam(CVID::MS_ms_level)->value == 2;
    }
};

//void validateWriteRead(const MSDataFile::WriteConfig& writeConfig, const DiffConfig diffConfig)
void validateWriteRead()
{
    String^ filenameBase_ = "temp.MSDataFileTest";

    //if (os_) *os_ << "validateWriteRead()\n  " << writeConfig << endl; 

    String^ filename1 = filenameBase_ + ".1";
    String^ filename2 = filenameBase_ + ".2";

    {
        // create MSData object in memory
        pwiz::CLI::msdata::MSData^ tiny = gcnew MSData();
        examples::initializeTiny(tiny);

        // write to file #1 (static)
        MSDataFile::write(tiny, filename1);

        // read back into an MSDataFile object
        MSDataFile^ msd1 = gcnew MSDataFile(filename1);

        MSData^ foob = gcnew MSData();
        ReaderList^ readers = ReaderList::FullReaderList;
        readers->read("c:/test/WIFFsForMatt/1_031105STlatterich_AQv1.4.1.wiff", foob);
        //delete readers;

        Console::WriteLine(foob->run->spectrumList->spectrum(0)->cvParam(CVID::MS_ms_level)->value);
        Console::WriteLine(foob->run->spectrumList->spectrum(123)->cvParam(CVID::MS_ms_level)->value);
        // compare
        //Diff<MSData> diff(tiny, msd1, diffConfig);
        //if (diff && os_) *os_ << diff << endl;
        //unit_assert(!diff);

        Spectrum^ s = msd1->run->spectrumList->spectrum(0);
        Console::WriteLine(s->cvParam(CVID::MS_ms_level)->value);
        //delete s;

        test^ foo = gcnew test();
        pwiz::CLI::analysis::SpectrumList_FilterAcceptSpectrum^ bar = gcnew pwiz::CLI::analysis::SpectrumList_FilterAcceptSpectrum(foo, &test::accept);
        SpectrumList^ sl = gcnew pwiz::CLI::analysis::SpectrumList_Filter(msd1->run->spectrumList, bar);
        Console::WriteLine(sl->spectrum(0)->cvParam(CVID::MS_ms_level)->value);
        //delete sl;

        // write to file #2 (member)
        msd1->write(filename2);
        //delete msd1;

        // read back into another MSDataFile object
        MSDataFile msd2(filename2);
        //delete msd2;

        // compare
        //diff(tiny, msd2);
        //if (diff && os_) *os_ << diff << endl;
        //unit_assert(!diff);
    }
    System::GC::Collect();
    //System::IO::File::WriteAllText("structor.log", ObjectStructorLog::Log->ToString());

    //Console::ReadLine();
    // remove temp files
    //System::IO::File::Delete(filename1);
    //System::IO::File::Delete(filename2);
}

/*void test()
{
    MSDataFile::WriteConfig writeConfig;
    DiffConfig diffConfig;

    // mzML 64-bit, full diff
    validateWriteRead(writeConfig, diffConfig);

    writeConfig.indexed = false;
    validateWriteRead(writeConfig, diffConfig); // no index
    writeConfig.indexed = true;

    // mzML 32-bit, full diff
    writeConfig.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;
    validateWriteRead(writeConfig, diffConfig);

    // mzXML 32-bit, diff ignoring metadata and chromatograms
    writeConfig.format = MSDataFile::Format_mzXML;
    diffConfig.ignoreMetadata = true;
    diffConfig.ignoreChromatograms = true;
    validateWriteRead(writeConfig, diffConfig);

    // mzXML 64-bit, diff ignoring metadata and chromatograms
    writeConfig.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_64;
    validateWriteRead(writeConfig, diffConfig);

    writeConfig.indexed = false;
    validateWriteRead(writeConfig, diffConfig); // no index
    writeConfig.indexed = true;
}


void demo()
{
    MSData tiny;
    examples::initializeTiny(tiny);

    MSDataFile::WriteConfig config;
    MSDataFile::write(tiny, filenameBase_ + ".64.mzML", config);

    config.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;
    MSDataFile::write(tiny, filenameBase_ + ".32.mzML", config);

    config.format = MSDataFile::Format_Text;
    MSDataFile::write(tiny, filenameBase_ + ".txt", config);

    config.format = MSDataFile::Format_mzXML;
    MSDataFile::write(tiny, filenameBase_ + ".32.mzXML", config);

    config.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_64;
    MSDataFile::write(tiny, filenameBase_ + ".64.mzXML", config);
}*/


int main(int argc, char* argv[])
{
    try
    {
        //if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        validateWriteRead();
        //test();
        //demo();
        //testReader();
        return 0;
    }
    catch (std::exception& e)
    {
        Console::Error->WriteLine("std::exception: " + gcnew String(e.what()));
    }
    catch (Exception^ e)
    {
        Console::Error->WriteLine("System.Exception: " + e->Message);
    }
    catch (...)
    {
        Console::Error->WriteLine("Caught unknown exception.\n");
    }
    
    return 1;
}

