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
/// PeptideHog.cpp
///

// Use to find proteins that consistently account for too many of the peptide IDs in a subset of runs

#include "pwiz/analysis/eharmony/PeptideID_dataFetcher.hpp"
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
    multimap<string,pair<string, double> > hoggers;
    vector<string> proteins;

    vector<string>::const_iterator file_it = config.filenames.begin();
    for(; file_it != config.filenames.end(); ++file_it)
        {
            double total = 0;
            map<string,double> protein2PeptideCounts;
            ifstream ifs(file_it->c_str());
            PeptideID_dataFetcher pidf(ifs);

            vector<boost::shared_ptr<SpectrumQuery> > sqs = pidf.getAllContents();
            vector<boost::shared_ptr<SpectrumQuery> >::iterator it = sqs.begin();
            for(; it != sqs.end(); ++it)
                {
                    string protein = (*it)->searchResult.searchHit.protein;
                    if (find(proteins.begin(), proteins.end(), protein) == proteins.end()) proteins.push_back(protein);

                    if (protein2PeptideCounts.find(protein) == protein2PeptideCounts.end())
                        {
                            protein2PeptideCounts.insert(pair<string,double>(protein,1));
                            
                        }

                    else
                        {
                            protein2PeptideCounts[protein] += 1;
                            
                        }

                    total +=1;
                    
                }
                    
            map<string,double>::iterator result_it = protein2PeptideCounts.begin();
            for(; result_it != protein2PeptideCounts.end(); ++result_it)
                {
                    result_it->second = result_it->second/total;
                    hoggers.insert(pair<string,pair<string, double> >(result_it->first, pair<string,double>(*file_it,result_it->second)));

                }

        }

    vector<string>::iterator prot_it =  proteins.begin();
    for(; prot_it != proteins.end(); ++prot_it)
        {
            pair<multimap<string,pair<string, double> >::iterator, multimap<string,pair<string, double> >::iterator> the_its = hoggers.equal_range(*prot_it);
            multimap<string,pair<string, double> >::iterator start = the_its.first;
            cout << start->first << "\t";
            int counter = 0;
            for(; start != the_its.second; ++start)
                {
                    counter += 1;

                }
            
            cout << counter << "\t";

            start = the_its.first;
            for(; start != the_its.second; ++start)
                {
                    cout << start->second.second << "\t";
                    
                }

            cout <<"\n";

        }

    return;

}

Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;
    Config config;

    ostringstream usage;
    usage << "Usage: PeptideHog run run run run run ... "
          << endl;

    const char* label_args = "args";

    po::options_description od_args;
    od_args.add_options()(label_args, po::value< vector<string> >(), "");

    po::positional_options_description pod_args;
    pod_args.add(label_args, -1);

    po::options_description od_parse;
    od_parse.add(od_args);

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
            cout << "[PeptideHog.cpp::main()] Abnormal termination.\n";
        }

    return 0;
}
