//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

///
/// PeptideVenn.cpp
///

// Use to analyse the similarity in peptide identifications between two runs

#include "pwiz/analysis/eharmony/PeptideID_dataFetcher.hpp"
#include "pwiz/analysis/eharmony/PeptideMatcher.hpp"
#include "boost/program_options.hpp"
#include "boost/filesystem/path.hpp"

#include <map>
#include <iterator>
#include <algorithm>
#include <string>
#include <iostream>
#include <fstream>

using namespace pwiz;
using namespace eharmony;
using namespace std;

struct Config
{
    Config(){}

    vector<string> filenames;

};

void go(const Config& config)
{
    map<string,double> peptideCounts;

    vector<string>::const_iterator file_it = config.filenames.begin();
    for(; file_it != config.filenames.end(); ++file_it)
        {
            ifstream ifs(file_it->c_str());
            PeptideID_dataFetcher pidf(ifs);         
        }

    ifstream ifs_a(config.filenames.begin()->c_str());
    boost::shared_ptr<PeptideID_dataFetcher> pidf_a(new PeptideID_dataFetcher(ifs_a,0));
    
    ifstream ifs_b(config.filenames.back().c_str());
    boost::shared_ptr<PeptideID_dataFetcher> pidf_b(new PeptideID_dataFetcher(ifs_b,0));

    ofstream ofs("peptideVenn.xml");
    PeptideMatcher pm(pidf_a, pidf_b);
    XMLWriter writer(ofs);

    PeptideMatchContainer matches = pm.getMatches();
    PeptideMatchContainer::iterator it = matches.begin();
    for( ; it != matches.end() ; ++it) { it->first->write(writer); it->second->write(writer);}

}

Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;
    Config config;

    ostringstream usage;
    usage << "Usage: PeptideVenn run1.pep.xml run2.pep.xml"
          << endl;

    const char* label_args = "args";

    po::options_description od_args;
    od_args.add_options()(label_args, po::value< vector<string> >(), "");

    po::positional_options_description pod_args;
    pod_args.add(label_args, -1);

    po::options_description od_parse;
    od_parse.add(od_args); //why do this .. what does od_args have that pod_args doesn't...i guess the od_args is the thing that is confusing me

    // parse command line
    po::variables_map vm;
    po::store(po::command_line_parser(argc, (char**)argv).options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    // get filenames
    if (vm.count(label_args))
        config.filenames = vm[label_args].as< vector<string> >();

    // usage if incorrect
    if (config.filenames.empty())
        throw runtime_error(usage.str());

    return config;

}

int main(int argc, const char* argv[])
{
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

    catch (...)
        {
            cout << "[PeptideVenn.cpp::main()] Abnormal termination.\n";
        }

    return 0;
}
