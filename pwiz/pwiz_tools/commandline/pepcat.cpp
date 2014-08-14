//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2010 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#include "pwiz/analysis/passive/PepXMLCat.hpp"

#include <exception>
#include <iostream>
#include <vector>
#include <boost/algorithm/string.hpp>
#include <boost/program_options.hpp>
#include <boost/filesystem.hpp>

using namespace std;
using namespace pwiz::analysis;
using namespace boost;

struct Config
{
    // TODO useful?
    vector<string> files;

    PepxmlRecordReader::Config pxConfig;
    
    string usageOptions;
};

Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;

    Config config;
    
    string usageOptions;

    po::options_description od_config("");
    od_config.add_options()
        ("delim,d",
         po::value<char>(&config.pxConfig.delim)->default_value('\t'),
         ": delimiter separating fields. Default is tab.")
        ("record,r",
         po::value<char>(&config.pxConfig.record)->default_value('\n'),
         ": delimiter separating records. Default is newline.")
        ("quote,q",
         po::value<char>(&config.pxConfig.quote)->default_value('"'),
         ": Character used for quoting text fields.")
        ("headers,h",
         ": Print headers.")
        ("quiet,q",
         ": Don't print headers.")
        ;
    
    ostringstream temp;
    temp << od_config;
    usageOptions = temp.str();

    const char* label_files = "files";
    po::options_description od_args1;
    od_args1.add_options()(label_files,
                           po::value< vector<string> >(&config.files),
                           "");

    po::positional_options_description pod_args;
    pod_args.add(label_files, -1);
    
    po::options_description od_parse;
    od_parse.add(od_config).add(od_args1);
    
    // parse command line

    po::variables_map vm;
    char** argv_hack = (char**)argv;
    po::store(po::command_line_parser(argc, argv_hack).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    if (vm.count("headers"))
        config.pxConfig.headers = true;

    if (vm.count("quiet"))
        config.pxConfig.headers = false;

    config.usageOptions = usageOptions;

    return config;
}

int main(int argc, const char* argv[])
{
    try
    {
        Config config = parseCommandLine(argc, argv);

        // TODO Change the demo file into commandline argument.
        //config.pxConfig.pepxmlfile = "/home/broter/DBDB_13_01.pep.xml";

        for (vector<string>::iterator i=config.files.begin();
             i!= config.files.end(); i++)
        {
            PepxmlRecordReader prr(config.pxConfig);

            // TODO - do something about headers.
            PepxmlRecordReader::const_iterator ci;
            cout << prr;
        }
        
        return 0;
    }
    catch (std::exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "[mzidtxt] Caught unknown exception.\n";
    }

    return 1;
}
