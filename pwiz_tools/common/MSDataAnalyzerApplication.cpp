//
// MSDataAnalyzerApplication.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/convenience.hpp"
#include "boost/program_options.hpp"
#include <fstream>


namespace pwiz {
namespace analysis {


using namespace std;


//
// MSDataAnalyzerApplication
//


PWIZ_API_DECL MSDataAnalyzerApplication::MSDataAnalyzerApplication(int argc, const char* argv[])
:   outputDirectory(".")
{
    namespace po = boost::program_options;

    string filelistFilename;
    string configFilename;

    po::options_description od_config("");
    od_config.add_options()
        ("outdir,o",
            po::value<string>(&outputDirectory)->default_value(outputDirectory),
            ": output directory")
        ("filelist,f",
            po::value<string>(&filelistFilename),
            ": text file containing filenames to process")
        ("config,c", 
            po::value<string>(&configFilename),
            ": configuration file (optionName=value)")
        ("exec,x", 
            po::value< vector<string> >(&commands)->composing(),
            ": execute command")
        ;

    // save options description

    ostringstream temp;
    temp << od_config;
    usageOptions = temp.str();

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

    // parse config file if required

    if (!configFilename.empty())
    {
        ifstream is(configFilename.c_str());
        po::store(parse_config_file(is, od_config), vm);
        po::notify(vm);
    }

    // remember filenames from command line

    if (vm.count(label_args))
        filenames = vm[label_args].as< vector<string> >();

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

    if (!filenames.empty())
        bfs::create_directories(outputDirectory);

    ExtendedReaderList readers;

    for (vector<string>::const_iterator it=filenames.begin(); it!=filenames.end(); ++it)
    {
        try
        {
            if (log) *log << "[MSDataAnalyzerApplication] Analyzing file: " << *it << endl;

            MSDataFile msd(*it, &readers);
            MSDataAnalyzer::DataInfo dataInfo(msd);
            dataInfo.sourceFilename = bfs::path(*it).leaf();
            dataInfo.outputDirectory = outputDirectory;
            dataInfo.log = log;

            MSDataAnalyzerDriver driver(analyzer);
            driver.analyze(dataInfo);
        }
        catch (exception& e)
        {
            if (log) *log << e.what() << "\n[MSDataAnalyzerApplication] Caught exception.\n";
        }
        catch (...)
        {
            if (log) *log << "[MSDataAnalyzerApplication] Caught unknown exception.\n";
        }
        
        if (log) *log << endl;
    }
}


} // namespace analysis 
} // namespace pwiz

