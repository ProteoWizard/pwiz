
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

#include "pwiz/data/identdata/IdentDataFile.hpp"
#include "pwiz/data/identdata/MascotReader.hpp"
#include "pwiz/data/identdata/TextWriter.hpp"
#include "pwiz/Version.hpp"

#include <boost/program_options.hpp>
#include <stdexcept>
#include <iostream>

using namespace std;
using namespace pwiz::identdata;

struct Config
{
    string mascotfile;
    string mzidfile;
    string usageOptions;

    vector<string> files;
};

string usage(const Config& config)
{
    ostringstream oss;
    
    oss << "Usage: mascot2mzid [-m] <mascot_filename> [-z] <mzid_filename>\n"
        << "Mascot to mzIdentML - translates a mascot \"dat\" file into an mzid file.\n"
        << "\n"
        << "Options:\n" 
        << "\n"
        << config.usageOptions
        << "\n";
    
    
    oss << endl
        << "Questions, comments, and bug reports:\n"
        << "https://github.com/ProteoWizard\n"
        << "support@proteowizard.org\n"
        << "\n"
        << "ProteoWizard release: " << pwiz::Version::str() << endl
        << "Build date: " << __DATE__ << " " << __TIME__ << endl;

    return oss.str();
}

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

    config.usageOptions = usageOptions;

    // parse command line

    po::options_description od_file_args;

    const char* label_files = "files";
    od_file_args.add_options()(label_files,
                               po::value< vector<string> >(&config.files),
                               "");

    po::positional_options_description pod_args;
    pod_args.add(label_files, -1);

    po::options_description od_parse;
    od_parse.add(od_config).add(od_file_args);

    po::variables_map vm;
    char** argv_hack = (char**)argv;
    po::store(po::command_line_parser(argc, argv_hack).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);
    
    if (vm.count(label_files))
    {
        if (vm.count("mascot") && vm.count("mzid"))
        {
            ostringstream oss;
            oss << "Too many input/output files\n";
            oss << usage(config);
            throw runtime_error(oss.str().c_str());
        }
        else if (vm.count("mascot") && !vm.count("mzid"))
        {
            config.mzidfile = vm[label_files].as< vector<string> >().at(0);
        }
        else if (!vm.count("mascot") && !vm.count("mzid") &&
                 vm[label_files].as< vector<string> >().size())
        {
            config.mascotfile = vm[label_files].as< vector<string> >().at(0);
            config.mzidfile = vm[label_files].as< vector<string> >().at(1);
        }
        else if (!vm.count("mascot") && vm.count("mzid"))
        {
            config.mascotfile = vm[label_files].as< vector<string> >().at(0);
        }
        else
            throw runtime_error(usage(config).c_str());
    }
    
    return config;
}

int main(int argc, const char**argv)
{
    try {
        Config config = processCommandline(argc, argv);

        if (config.mascotfile.empty() || config.mzidfile.empty())
            throw invalid_argument(usage(config));
        
        ReaderPtr  mr(new MascotReader());
        IdentData mzid;
        mr->read(config.mascotfile, mzid, Reader::Config());

        IdentDataFile::write(mzid, config.mzidfile);
        
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
