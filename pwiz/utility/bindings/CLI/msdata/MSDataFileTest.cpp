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
#include "../common/unit.hpp"


using namespace pwiz::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::data;
using namespace pwiz::CLI::msdata;
using namespace pwiz::CLI::util;
using namespace System::Collections::Generic;
using System::String;
using System::Console;


public ref class Log
{
    public: static System::IO::TextWriter^ writer = nullptr;
};


void hackInMemoryMSData(MSData^ msd)
{
    // remove metadata ptrs appended on read
    SourceFileList^ sfs = msd->fileDescription->sourceFiles;
    if (sfs->Count > 0) sfs->RemoveAt(sfs->Count-1);
    SoftwareList^ sws = msd->softwareList;
    if (sws->Count > 0) sws->RemoveAt(sws->Count-1);

    if (msd->run->spectrumList != nullptr) msd->run->spectrumList->setDataProcessing(nullptr);
    if (msd->run->chromatogramList != nullptr) msd->run->chromatogramList->setDataProcessing(nullptr);
}


public ref struct IterationListenerCollector : IterationListener
{
    static List<UpdateMessage^>^ updateMessages = gcnew List<UpdateMessage^>();

    virtual Status update(UpdateMessage^ updateMessage) override
    {
        updateMessages->Add(updateMessage);
        if (Log::writer != nullptr)
            Console::WriteLine("{0} {1}/{2}",
                               updateMessage->message,
                               updateMessage->iterationIndex,
                               updateMessage->iterationCount);
        return Status::Ok;
    }
};


//void validateWriteRead(const MSDataFile::WriteConfig& writeConfig, const DiffConfig diffConfig)
void validateWriteRead(IterationListenerRegistry^ ilr)
{
    MSDataFile::WriteConfig writeConfig;
    DiffConfig diffConfig;

    String^ filenameBase_ = "temp.MSDataFileTest";

    //if (os_) *os_ << "validateWriteRead()\n  " << writeConfig << endl; 

    String^ filename1 = filenameBase_ + "_CLI.1";
    String^ filename2 = filenameBase_ + "_CLI.2";
    String^ filename3 = filenameBase_ + ToSystemString("_CLI.\xE4\xB8\x80\xE4\xB8\xAA\xE8\xAF\x95.4");
    // FIXME: 4-byte UTF-8 not working: String^ filename4 = filenameBase_ + "_CLI.\x01\x04\xA4\x01\x04\xA2.5";

    {
        // create MSData object in memory
        MSData tiny;
        examples::initializeTiny(%tiny);

        // write to file #1 (static)
        MSDataFile::write(%tiny, filename1, %writeConfig, ilr);

        // read back into an MSDataFile object
        MSDataFile^ msd1 = gcnew MSDataFile(filename1, ilr);
        hackInMemoryMSData(msd1);

        // compare
        Diff diff(%tiny, msd1, %diffConfig);
        if ((bool)diff && Log::writer != nullptr) Log::writer->WriteLine((String^)diff);
        unit_assert(!(bool)diff);


        // write to file #2 (member)
        msd1->write(filename2, %writeConfig, ilr);

        // read back into another MSDataFile object
        MSDataFile^ msd2 = gcnew MSDataFile(filename2, ilr);
        hackInMemoryMSData(msd2);

        // compare
        diff.apply(%tiny, msd2);
        if ((bool)diff && Log::writer != nullptr) Log::writer->WriteLine((String^)diff);
        unit_assert(!(bool)diff);


        // write to file #3 (testing conversion of .NET UTF-16 to 2-byte UTF-8)
        msd1->write(filename3, %writeConfig);

        // read back into another MSDataFile object
        MSDataFile^ msd3 = gcnew MSDataFile(filename3);
        hackInMemoryMSData(msd3);

        // compare
        diff.apply(%tiny, msd3);
        if ((bool)diff && Log::writer != nullptr) Log::writer->WriteLine((String^)diff);
        unit_assert(!(bool)diff);


        // write to file #4 (testing conversion of .NET UTF-16 to 4-byte UTF-8)
        /*msd1->write(filename4, %writeConfig, ilr);

        // read back into another MSDataFile object
        MSDataFile^ msd4 = gcnew MSDataFile(filename4, ilr);
        hackInMemoryMSData(msd4);

        // compare
        diff.apply(%tiny, msd4);
        if ((bool)diff && Log::writer != nullptr) Log::writer->WriteLine((String^)diff);
        unit_assert(!(bool)diff);*/


        delete msd1; // calls Dispose()
        delete msd2;
        delete msd3;
        //delete msd4;
    }

    // remove temp files
    System::IO::File::Delete(filename1);
    System::IO::File::Delete(filename2);
    System::IO::File::Delete(filename3);
    //System::IO::File::Delete(filename4);

    unit_assert(IterationListenerCollector::updateMessages->Count >= 12); // 14 iterations, 2 iterations between updates (or index+1==count)
    unit_assert(IterationListenerCollector::updateMessages[0]->iterationCount == 5); // 5 spectra
    unit_assert(IterationListenerCollector::updateMessages[5]->iterationCount == 2); // 2 chromatograms
    unit_assert(IterationListenerCollector::updateMessages[0]->iterationIndex == 0);
    unit_assert(IterationListenerCollector::updateMessages[3]->iterationIndex == 4);
    unit_assert(IterationListenerCollector::updateMessages[4]->iterationIndex == 0);
    unit_assert(IterationListenerCollector::updateMessages[5]->iterationIndex == 1);
    unit_assert(IterationListenerCollector::updateMessages[6]->iterationIndex == 0);

    // create MSData object in memory
    MSData tiny;
    examples::initializeTiny(%tiny);

    // write to file #1 (static)
    MSDataFile::write(%tiny, filename1, %writeConfig, ilr);

    for (int i=0; i < 100; ++i)
    {
        // read back into an MSDataFile object
        MSDataFile^ msd1 = gcnew MSDataFile(filename1, ilr);
        hackInMemoryMSData(msd1);

        // compare
        Diff diff(%tiny, msd1, %diffConfig);
        if ((bool)diff && Log::writer != nullptr) Log::writer->WriteLine((String^)diff);
        unit_assert(!(bool)diff);
    }
    
    System::GC::Collect();
    System::GC::WaitForPendingFinalizers();

    // remove temp files
    System::IO::File::Delete(filename1);
    System::IO::File::Delete(filename2);
    System::IO::File::Delete(filename3);
    //System::IO::File::Delete(filename4);
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
    TEST_PROLOG_EX(argc, argv, "_CLI")

    try
    {
        IterationListenerRegistry^ ilr = gcnew IterationListenerRegistry();
        ilr->addListener(gcnew IterationListenerCollector(), 2);

        if (argc>1 && !strcmp(argv[1],"-v"))  Log::writer = Console::Out;

        validateWriteRead(ilr);
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

