//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#include "MSDataAnalyzerApplication.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <boost/filesystem/operations.hpp>
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::analysis;


ostream* os_ = 0;


struct DummyAnalyzer : public MSDataAnalyzer
{
    vector<string> filenames;

    virtual void open(const DataInfo& dataInfo) 
    {
        filenames.push_back(dataInfo.sourceFilename);
    }
};


const char* tempFilename_ = "MSDataAnalyzerApplicationTest.temp.txt";


void test()
{
    // cleanup failed tests
    try {boost::filesystem::remove(tempFilename_);} catch(...) {}

    if (os_) *os_ << "test()\n\n"; 

    const char* argv[] = 
    {
        "executable_name",
        "file0",
        "-o", "output_directory_name",
        "file1",
        "-x", "command0",
        "-f", "filelist_name",
        "file2",
        "file3",
        "file4",
        "--filter", "peakPicking true 1-",
        "-x", "command1",
        "-x", "command2",
        tempFilename_,
        "--filter", "index 0-"
    };

    int argc = sizeof(argv)/sizeof(const char*);

    MSDataAnalyzerApplication app(argc, argv);

    if (os_) 
    {
        *os_ << "usageOptions:\n" << app.usageOptions << endl;
        *os_ << "outputDirectory: " << app.outputDirectory << "\n\n";
        *os_ << "filenames:\n";
        copy(app.filenames.begin(), app.filenames.end(), ostream_iterator<string>(*os_, "\n"));
        *os_ << endl;
        *os_ << "commands:\n";
        copy(app.commands.begin(), app.commands.end(), ostream_iterator<string>(*os_, "\n"));
        *os_ << endl;
        *os_ << "filters:\n";
        copy(app.filters.begin(), app.filters.end(), ostream_iterator<string>(*os_, "\n"));
        *os_ << endl;
    }

    unit_assert(app.filenames.size() == 6);
    unit_assert(app.commands.size() == 3);
    unit_assert(app.filters.size() == 2);

    MSData temp;
    examples::initializeTiny(temp);
    MSDataFile::write(temp, tempFilename_);

    if (os_) *os_ << "Running app with dummy analyzer:\n";
    DummyAnalyzer dummy;
    app.outputDirectory = "."; // don't actually create "output_directory_name"
    app.run(dummy, os_);

    if (os_)
    {
        *os_ << endl;
        *os_ << "dummy filenames:\n";
        copy(dummy.filenames.begin(), dummy.filenames.end(), ostream_iterator<string>(*os_, "\n"));
        *os_ << endl;
    }

    unit_assert(dummy.filenames.size() == 1);

    boost::filesystem::remove(tempFilename_);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}

