//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


// #include "TraDataFile.hpp"
// #include "Diff.hpp"
// #include "IO.hpp"
// #include "examples.hpp"
#include "../common/unit.hpp"


using namespace System;
using namespace pwiz::CLI::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::data;
using namespace pwiz::CLI::tradata;


public ref class Log
{
    public: static System::IO::TextWriter^ writer = nullptr;
};


string filenameBase_ = "temp.TraDataFileTest";

void hackInMemoryTraData(TraData& td)
{
     SoftwareList^ sws = td.softwarePtrs;
    if (sws->Count > 0) sws->RemoveAt(sws->Count-1);
}

void test()
{
     TraDataFile::WriteConfig writeConfig;

	//if (os_) *os_ << "test()\n  " << writeConfig << endl; 

    string filename1 = filenameBase_ + ".1";
    string filename2 = filenameBase_ + ".2";

    {
        // create TraData object in memory
        TraData tiny;
        examples::initializeTiny(%tiny);

        // write to file #1 (static)
        TraDataFile::write(%tiny, filename1, %writeConfig);

        // read back into an TraDataFile object
        TraDataFile td1(filename1);
        hackInMemoryTraData(%tiny);

        // compare
        Diff diff(tiny, td1);
        if ((bool)diff && Log::writer != nullptr) Log::writer->WriteLine((string)diff);
        unit_assert(!(bool)diff);

        // write to file #2 (member)
        td1.write(filename2, %writeConfig);

        // read back into another TraDataFile object
        TraDataFile td2(filename2);
        hackInMemoryTraData(%td2);

        // compare
        diff.apply(tiny, td2);
        if ((bool)diff && Log::writer != nullptr) Log::writer->WriteLine((string)diff);
        unit_assert(!(bool)diff);

	}

    // remove temp files
    boost::filesystem::remove(filename1);
    boost::filesystem::remove(filename2);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_CLI")

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