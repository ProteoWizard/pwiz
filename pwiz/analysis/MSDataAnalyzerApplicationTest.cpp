//
// MSDataAnalyzerApplicationTest.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "MSDataAnalyzerApplication.hpp"
#include "msdata/MSDataFile.hpp"
#include "util/unit.hpp"
#include <iostream>
#include <iterator>


using namespace std;
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
        "-x", "command1",
        "-x", "command2",
        tempFilename_
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
    }

    unit_assert(app.filenames.size() == 6);
    unit_assert(app.commands.size() == 3);

    MSData temp;
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

    system(("rm " + string(tempFilename_)).c_str());
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}

