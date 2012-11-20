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


//#include "ProteomeDataFile.hpp"
//#include "../../../data/proteome/Diff.hpp"
//#include "examples.hpp"
#include "../common/unit.hpp"


using namespace pwiz::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::proteome;
using System::Console;


public ref class Log
{
    public: static System::IO::TextWriter^ writer = nullptr;
};


//void validateWriteRead(const ProteomeDataFile::WriteConfig& writeConfig, const DiffConfig diffConfig)
void validateWriteRead()
{
    ProteomeDataFile::WriteConfig writeConfig(ProteomeDataFile::Format::Format_FASTA);
    DiffConfig diffConfig;

    string filenameBase_ = "temp.ProteomeDataFileTest";

    //if (os_) *os_ << "validateWriteRead()\n  " << writeConfig << endl; 

    string filename1 = filenameBase_ + ".1";
    string filename2 = filenameBase_ + ".2";

    {
        // create ProteomeData object in memory
        ProteomeData tiny;
        examples::initializeTiny(%tiny);

        // write to file #1 (static)
        ProteomeDataFile::write(%tiny, filename1, %writeConfig);

        // read back into an ProteomeDataFile object
        ProteomeDataFile msd1(filename1);

        // compare
        Diff diff(tiny, msd1, %diffConfig);
        if ((bool)diff && Log::writer != nullptr) Log::writer->WriteLine((string)diff);
        unit_assert(!(bool)diff);

        // write to file #2 (member)
        msd1.write(filename2, %writeConfig);

        // read back into another ProteomeDataFile object
        ProteomeDataFile msd2(filename2);

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
    ProteomeDataFile::WriteConfig writeConfig;
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
    writeConfig.format = ProteomeDataFile::Format_mzXML;
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
    ProteomeData tiny;
    examples::initializeTiny(tiny);

    ProteomeDataFile::WriteConfig config;
    ProteomeDataFile::write(tiny, filenameBase_ + ".64.mzML", config);

    config.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;
    ProteomeDataFile::write(tiny, filenameBase_ + ".32.mzML", config);

    config.format = ProteomeDataFile::Format_Text;
    ProteomeDataFile::write(tiny, filenameBase_ + ".txt", config);

    config.format = ProteomeDataFile::Format_mzXML;
    ProteomeDataFile::write(tiny, filenameBase_ + ".32.mzXML", config);

    config.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_64;
    ProteomeDataFile::write(tiny, filenameBase_ + ".64.mzXML", config);
}*/


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_CLI")

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) Log::writer = Console::Out;
        validateWriteRead();
        //test();
        //demo();
        //testReader();
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

