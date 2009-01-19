//
// Original Author: Parag Mallick
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
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



#include "PrecursorCalculator.hpp"
#include "msaux/DebugTimer.h"
#include "msaux/MSRun.hpp"
#include "msaux/MSRunWriter.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/convenience.hpp"
#include "boost/program_options.hpp"
#include <iostream>
#include <sstream>
#include <fstream>
#include <stdexcept>
#include <vector>


using namespace mstools::msaux;
using namespace mstools::msaux::msrun;
using namespace std;


struct Config
{
    vector<string> filenames;
    string outputPath;
    string extension;
    bool calculatePrecursors;

    Config()
    :   outputPath("."), 
        extension("_msprefix.mzxml"),
        calculatePrecursors(true)
    {}

    string outputFilename(const string& inputFilename) const;
};


string Config::outputFilename(const string& filename) const
{
    namespace bfs = boost::filesystem;
    string newFilename = bfs::basename(filename) + this->extension;
    bfs::path fullPath = bfs::path(this->outputPath) / newFilename;
    return fullPath.string(); 
}


void processFile(const string& filename, const Config& config)
{
    cout << "\nProcessing file: " << filename << endl; 
    
    auto_ptr<MSRun> msrunBase = MSRun::create(filename);
    const MSRun* msrun = msrunBase.get(); 

    auto_ptr<PrecursorCalculator> pc;
    if (config.calculatePrecursors)
    {    
        pc = PrecursorCalculator::create(*msrun);
        msrun = pc.get();
    }

    // debug
	/*
    int scanCount = pc->scanCount();
    for (int i=1; i<=scanCount; i++)
    {
        PrecursorCalculator::Result precursor = pc->calculate(i);
        cout << i << " " << pc->msLevel(i) << " " << precursor << " " << endl;
    }
    */
    // debug

    auto_ptr<MSRunWriter> writer = 
        MSRunWriter::create(msrun, config.outputFilename(filename));
    writer->write();
}


void go(const Config& config)
{
    cout << "outputPath: " << config.outputPath << endl;
    cout << "extension: " << config.extension << endl;
    cout << "calculatePrecursors: " << boolalpha << config.calculatePrecursors << endl;

    namespace bfs = boost::filesystem;
    bfs::create_directories(config.outputPath);

    for (vector<string>::const_iterator it=config.filenames.begin(); it!=config.filenames.end(); ++it)
    {
        try
        {
            processFile(*it, config);
        }
        catch (exception& e)
        {
            cout << e.what() << endl;
            cout << "Error processing file " << *it << endl; 
        }
        catch (msrun::MSRunException& e)
        {
            cout << e.message() << endl;
            cout << "Error processing file " << *it << endl; 
        }
        catch (...)
        {
            cout << "Unknown error.\n";
            cout << "Error processing file " << *it << endl; 
        }
    }
}


Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;

    ostringstream usage;
    usage << "Usage: msprefix [options] [files]+\n"
          << "MassSpecPreFix: Preprocess a RAW/mzXML file.\n"
          << endl;

    Config config;
    string filelistFilename;

    po::options_description od_config("Options");
    od_config.add_options()
        ("filelist,f",
            po::value<string>(&filelistFilename),
            ": specify text file containing filenames")
        ("outdir,o",
            po::value<string>(&config.outputPath)->default_value(config.outputPath),
            ": set output directory")
        ("ext,e",
            po::value<string>(&config.extension)->default_value(config.extension),
			": set extension for output files")
		("precursors,p", 
            po::value<bool>(&config.calculatePrecursors)->default_value(config.calculatePrecursors),
            ": calculate precursors using FT data")
        ;

    // append options description to usage string

    usage << od_config;

    // handle positional arguments

    const char* label_args = "args";

    po::options_description od_args;
    od_args.add_options()(label_args, po::value< vector<string> >(), "");

    po::positional_options_description pod_args;
    pod_args.add(label_args, -1);
   
    po::options_description od_parse;
    od_parse.add(od_config).add(od_args);

    // parse command line

    po::variables_map vm;
//    char** argv_hack = (char**)argv;
    po::store(po::command_line_parser(argc, (char**)argv).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    // remember filenames from command line

    if (vm.count(label_args))
        config.filenames = vm[label_args].as< vector<string> >();

    // parse filelist if required

    if (!filelistFilename.empty())
    {
        ifstream is(filelistFilename.c_str());
        while (is)
        {
            string filename;
            getline(is, filename);
            if (is) config.filenames.push_back(filename);
        }
    }

    // check stuff

    if (config.filenames.empty())
        throw runtime_error(usage.str());

    return config;
}


int main(int argc, const char* argv[])
{
    DebugTimer timer;

    namespace bfs = boost::filesystem;
    bfs::path::default_name_check(bfs::native);

    try
    {
        Config config = parseCommandLine(argc, argv);        
        go(config);
        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
    }
    catch (msrun::MSRunException& e)
    {
        cout << e.message() << endl; 
    }
    catch (...)
    {
        cout << "[msprefix.cpp::main()] Abnormal termination.\n";
    }

    return 1;
}

