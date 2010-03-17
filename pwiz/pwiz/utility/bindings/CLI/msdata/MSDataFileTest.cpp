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


//#include "MSDataFile.hpp"
//#include "../../../data/msdata/Diff.hpp"
//#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"


using namespace pwiz::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::data;
using namespace pwiz::CLI::msdata;
using System::Console;
typedef System::String^ string;


public ref class Log
{
    public: static System::IO::TextWriter^ writer = nullptr;
};


void hackInMemoryMSData(MSData^ writtenData, MSData^ readData)
{
    // remove metadata ptrs appended on read
    SourceFileList^ sfs = readData->fileDescription->sourceFiles;
    if (sfs->Count > 0) sfs->RemoveAt(sfs->Count-1);
    SoftwareList^ sws = readData->softwareList;
    if (sws->Count > 0) sws->RemoveAt(sws->Count-1);

    // make written and read DataProcessing match
    SpectrumList^ sl = writtenData->run->spectrumList;
    if (sl != nullptr)
    {
        SpectrumList^ rsl = readData->run->spectrumList;
        DataProcessing^ dp = sl->dataProcessing();
        DataProcessing^ rdp = sl->dataProcessing();
        if (dp != nullptr)
        {
            dp->id = "";
            rdp->id = "";
            dp->processingMethods->Clear();
            rdp->processingMethods->Clear();
        }
    }

    ChromatogramList^ cl = writtenData->run->chromatogramList;
    if (cl != nullptr)
    {
        ChromatogramList^ rcl = readData->run->chromatogramList;
        DataProcessing^ dp = cl->dataProcessing();
        DataProcessing^ rdp = rcl->dataProcessing();
        if (dp != nullptr)
        {
            dp->id = "";
            rdp->id = "";
            dp->processingMethods->Clear();
            rdp->processingMethods->Clear();
        }
    }
}


//void validateWriteRead(const MSDataFile::WriteConfig& writeConfig, const DiffConfig diffConfig)
void validateWriteRead()
{
    MSDataFile::WriteConfig writeConfig(MSDataFile::Format::Format_mzML);
    DiffConfig diffConfig;

    string filenameBase_ = "temp.MSDataFileTest";

    //if (os_) *os_ << "validateWriteRead()\n  " << writeConfig << endl; 

    string filename1 = filenameBase_ + ".1";
    string filename2 = filenameBase_ + ".2";

    {
        // create MSData object in memory
        MSData tiny;
        examples::initializeTiny(%tiny);

        // write to file #1 (static)
        MSDataFile::write(%tiny, filename1, %writeConfig);

        // read back into an MSDataFile object
        MSDataFile msd1(filename1);
        hackInMemoryMSData(%tiny, %msd1);

        // compare
        Diff diff(tiny, msd1, %diffConfig);
        if ((bool)diff && Log::writer != nullptr) Log::writer->WriteLine((string)diff);
        unit_assert(!(bool)diff);

        // write to file #2 (member)
        msd1.write(filename2, %writeConfig);

        // read back into another MSDataFile object
        MSDataFile msd2(filename2);
        hackInMemoryMSData(%tiny, %msd2);

        // compare
        diff.apply(tiny, msd2);
        if ((bool)diff && Log::writer != nullptr) Log::writer->WriteLine((string)diff);
        unit_assert(!(bool)diff);
    }

    // remove temp files
    System::IO::File::Delete(filename1);
    System::IO::File::Delete(filename2);
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
        if (argc>1 && !strcmp(argv[1],"-v")) Log::writer = Console::Out;
        validateWriteRead();
        //test();
        //demo();
        //testReader();
        return 0;
    }
    catch (std::exception& e)
    {
        Console::Error->WriteLine("std::exception: " + gcnew System::String(e.what()));
    }
    catch (System::Exception^ e)
    {
        Console::Error->WriteLine("System.Exception: " + e->Message);
    }
    catch (...)
    {
        Console::Error->WriteLine("Caught unknown exception.\n");
    }
    
    return 1;
}

