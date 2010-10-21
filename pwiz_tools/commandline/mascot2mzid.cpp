//
// $Id$
//
//
// Origional author: Robert Burke <robert.burke@proteowizard.org>
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

#include "pwiz/data/mziddata/MzIdentMLFile.hpp"
#include "pwiz/data/mziddata/MascotReader.hpp"
#include "pwiz/data/mziddata/TextWriter.hpp"

#include <boost/program_options.hpp>
#include <stdexcept>
#include <iostream>

using namespace std;
using namespace pwiz::mziddata;

struct Config
{
    string mascotfile;
    string mzidfile;
    string usageOptions;
};

Config processCommandline(int argc, const char** argv)
{
    namespace po = boost::program_options;

    Config config;

    string usageOptions;

    po::options_description od_config("");
    od_config.add_options()
        ("mascot,m",
         po::value<string>(&config.mascotfile))
        ("mzid,z",
         po::value<string>(&config.mzidfile))
        ;
    
    ostringstream temp;
    temp << od_config;
    usageOptions = temp.str();

    // parse command line

    const char* label_files = "files";
    vector<string> args;

    po::positional_options_description pod_args;
    pod_args.add(label_files, -1);

    po::options_description od_parse;
    od_parse.add(od_config);

    po::variables_map vm;
    char** argv_hack = (char**)argv;
    po::store(po::command_line_parser(argc, argv_hack).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    config.usageOptions = usageOptions;

    return config;
}

int main(int argc, const char**argv)
{
    try {
        Config config = processCommandline(argc, argv);

        if (config.mascotfile.empty() || config.mzidfile.empty())
            throw invalid_argument(config.usageOptions);
        
        ReaderPtr  mr(new MascotReader());
        MzIdentML mzid;
        mr->read(config.mascotfile, mzid);

        MzIdentMLFile::write(mzid, config.mzidfile);
        
        // DEBUG begin
        //TextWriter tw(cout);
        //tw(mzid);
        // DEBUG end
        
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}
