//
// mspicture.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
// Modifieing author: Robert Burke <robert.burke@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#include "pwiz/analysis/passive/MSDataAnalyzerApplication.hpp"
#include "pwiz/analysis/passive/MSDataCache.hpp"
#include "pwiz/analysis/passive/MetadataReporter.hpp"
#include "data/msdata/MSDataFile.hpp"
#include "pwiz/analysis/passive/Pseudo2DGel.hpp"
#include "pwiz/analysis/peptideid/PeptideID_pepXML.hpp"

#include <iostream>
#include <fstream>
#include <iterator>
#include <stdexcept>
#include <boost/program_options.hpp>
#include <boost/filesystem.hpp>


using namespace pwiz::analysis;
using boost::shared_ptr;
using namespace std;
using namespace pwiz::peptideid;
using namespace boost::filesystem;

struct Config
{
    vector<string> filenames;
    string configFilename;
    string outputDirectory;
    string usageOptions;
    vector<string> commands;

    Config() : outputDirectory(".") {}
};

template <typename analyzer_type>
void printCommandUsage(ostream& os)
{
    os << "  " << analyzer_strings<analyzer_type>::id()
       << " " << analyzer_strings<analyzer_type>::argsFormat() << endl
       << "    (" << analyzer_strings<analyzer_type>::description() << ")\n";

    vector<string> usage = analyzer_strings<analyzer_type>::argsUsage();
    for (vector<string>::const_iterator it=usage.begin(); it!=usage.end(); ++it)
        os << "      " << *it << endl;

    os << endl;
}

string usage(const Config& config)
{
    ostringstream oss;

    oss << "Usage: mspaint [options] [mzxml_filename]\n"
        << "MassSpecPaint - command line access to mass spec data files with pep.xml annotation\n"
        << "\n"
        << "Options:\n" 
        << "\n"
        << config.usageOptions
        << "\n"
        << "Commands:\n"
        << "\n";

    printCommandUsage<Pseudo2DGel>(oss);

    oss << endl
        << "Questions, comments, and bug reports:\n"
        << "http://proteowizard.sourceforge.net\n"
        << "support@proteowizard.org\n";

    return oss.str();
}

Config parseCommandArgs(int argc, const char* argv[])
{
    namespace po = boost::program_options;

    Config config;
    
    string usageOptions;

    po::options_description od_config("");
    od_config.add_options()
        ("outdir,o",
            po::value<string>(&config.outputDirectory)->default_value(
                config.outputDirectory),
            ": output directory")
        ("config,c", 
            po::value<string>(&config.configFilename),
            ": configuration file (optionName=value) (ignored)")
        ("exec,x", 
            po::value< vector<string> >(&config.commands)->composing(),
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

    // remember filenames from command line

    if (vm.count(label_args))
        config.filenames = vm[label_args].as< vector<string> >();

    config.usageOptions = usageOptions;

    return config;
}

void initializeAnalyzers(MSDataAnalyzerContainer& analyzers,
                         const vector<string>& commands)
{
    shared_ptr<MSDataCache> cache(new MSDataCache);
    analyzers.push_back(cache);
    
    for (vector<string>::const_iterator it=commands.begin(); it!=commands.end(); ++it)
    {
        string name, args;
        istringstream iss(*it);
        iss >> name;
        getline(iss, args);

        if (name == analyzer_strings<Pseudo2DGel>::id())
        {
            MSDataAnalyzerPtr anal(new Pseudo2DGel(*cache, args));
            analyzers.push_back(anal);
        }
        else
        {
            MSDataAnalyzerPtr anal(new Pseudo2DGel(*cache, *it));
            analyzers.push_back(anal);
        }

        break;
    }
}

int main(int argc, const char* argv[])
{
    namespace bfs = boost::filesystem;
    try
    {
        Config config = parseCommandArgs(argc, argv);

        if (config.filenames.empty())
            throw runtime_error(usage(config).c_str());

        if (!config.filenames.empty())
            bfs::create_directories(config.outputDirectory);
        
        // Construct the Pseudo2DGel object with an MSDataCache object
        shared_ptr<Pseudo2DGel> analyzer;
        
        MSDataAnalyzerContainer analyzers;
        initializeAnalyzers(analyzers, config.commands);
        
        // Only take the first file for now.
        MSDataFile msd(config.filenames[0]);
        MSDataAnalyzer::DataInfo dataInfo(msd);
        
        dataInfo.sourceFilename = path(config.filenames[0]).leaf();
        dataInfo.outputDirectory = config.outputDirectory;
        dataInfo.log = NULL;

        MSDataAnalyzerDriver driver(analyzers);

        driver.analyze(dataInfo);

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


