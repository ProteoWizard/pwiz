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


#define PWIZ_SOURCE

#include "MSDataAnalyzerApplication.hpp"
#include "FullReaderList.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "boost/program_options.hpp"


namespace pwiz {
namespace analysis {


using namespace pwiz::util;


//
// MSDataAnalyzerApplication
//


PWIZ_API_DECL MSDataAnalyzerApplication::MSDataAnalyzerApplication(int argc, const char* argv[])
:   outputDirectory("."), verbose(false)
{
    namespace po = boost::program_options;

    string filelistFilename;
    string configFilename;
    bool detailedHelp = false;

    po::options_description od_config("");
    od_config.add_options()
        ("filelist,f",
            po::value<string>(&filelistFilename),
            ": text file containing filenames to process")
        ("outdir,o",
            po::value<string>(&outputDirectory)->default_value(outputDirectory),
            ": output directory")
        ("config,c", 
            po::value<string>(&configFilename),
            ": configuration file (containing settings as optionName=value)")
        ("exec,x", 
            po::value< vector<string> >(&commands),
            ": execute command, e.g --exec \"tic mz=409-412\"")
        ("filter",
            po::value< vector<string> >(&filters),
            ": add a spectrum list filter, e.g. --filter=\"msLevel [2,3]\"")
        ("verbose,v",
            po::value<bool>(&verbose)->zero_tokens(),
            ": print progress messages")
        ("help",
            po::value<bool>(&detailedHelp)->zero_tokens(),
            ": show this message, with extra detail on filter options")
        ;



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
    char** argv_hack = (char**)argv;
    po::store(po::command_line_parser(argc, argv_hack).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    // save options description

    ostringstream usage;
    usage << od_config;
    
    // extra usage for filters
    usage << SpectrumListFactory::usage(detailedHelp,"run this command with --help to see more detail") << endl;
    usageOptions = usage.str();

	// parse config file if required

    if (!configFilename.empty())
    {
        ifstream is(configFilename.c_str());
        po::store(parse_config_file(is, od_config), vm);
        po::notify(vm);
    }

    // remember filenames from command line

    if (vm.count(label_args))
    {
        filenames = vm[label_args].as< vector<string> >();

        // expand the filenames by globbing to handle wildcards
        vector<bfs::path> globbedFilenames;
        BOOST_FOREACH(const string& filename, filenames)
        {
            if (0==expand_pathmask(bfs::path(filename), globbedFilenames))
            {
                cout <<  "[MSDataAnalyzerApplication] no files found matching \"" << filename << "\"" << endl;
                globbedFilenames.push_back(filename); // this ought to provoke an error downstream
            }
        }

        if (!globbedFilenames.empty())
        {
            filenames.clear();
            BOOST_FOREACH(const bfs::path& filename, globbedFilenames)
                filenames.push_back(filename.string());
        }
    }

    // parse filelist if required

    if (!filelistFilename.empty())
    {
        ifstream is(filelistFilename.c_str());
        while (is)
        {
            string filename;
            getline(is, filename);
            if (is) filenames.push_back(filename);
        }
    }
}


PWIZ_API_DECL void MSDataAnalyzerApplication::run(MSDataAnalyzer& analyzer, ostream* log) const
{
    namespace bfs = boost::filesystem;

    if (!filenames.empty() && !bfs::exists(outputDirectory))
        bfs::create_directories(outputDirectory);

    FullReaderList readers;
    ostream* errorlog = log;

    if (!verbose)
        log = NULL;

    BOOST_FOREACH(const string& filename, filenames)
    {
        try
        {
            if (log) *log << "[MSDataAnalyzerApplication] Analyzing file: " << filename << endl;

            MSDataFile msd(filename, &readers);
            SpectrumListFactory::wrap(msd, filters);

            MSDataAnalyzer::DataInfo dataInfo(msd);
            dataInfo.sourceFilename = BFS_STRING(bfs::path(filename).leaf());
            dataInfo.outputDirectory = outputDirectory;
            dataInfo.log = log;

            MSDataAnalyzerDriver driver(analyzer);
            driver.analyze(dataInfo);
        }
        catch (exception& e)
        {
            if (errorlog) *errorlog << e.what() << "\n[MSDataAnalyzerApplication] Caught exception for file " << filename << ".\n";
        }
        catch (...)
        {
            if (errorlog) *errorlog << "[MSDataAnalyzerApplication] Caught unknown exception for file " << filename << ".\n";
        }
        
        if (log) *log << endl;
    }
}


} // namespace analysis 
} // namespace pwiz

