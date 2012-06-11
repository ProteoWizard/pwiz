//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@cshs.org>
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

#include "pwiz/analysis/peptideid/PeptideID_pepXML.hpp"
#include "pwiz/analysis/peptideid/PeptideID_flat.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/program_options.hpp>
#include <boost/filesystem.hpp>


using namespace pwiz::peptideid;
using namespace boost::filesystem;

struct Config
{
    vector<string> filenames;
    string configFilename;
    string outputFilename;
    string usageOptions;
    vector<string> commands;
    
    shared_ptr<PeptideID> peptide_id;

    Config() {}
};

struct Tally
{
    vector<string> peptides;
    vector<string> proteins;
    vector<string> uniquePeptides;
    vector<string> uniqueProteins;

    size_t size() const { return peptides.size(); }
};

string usage(const Config& config)
{
    ostringstream oss;
    
    oss << "Usage: pepsum [pepxml_filename]\n"
        << "\n";

    oss << endl
        << "Questions, comments, and bug reports:\n"
        << "http://proteowizard.sourceforge.net\n"
        << "support@proteowizard.org\n"
        << "\n"
        << "Build date: " << __DATE__ << " " << __TIME__ << endl;

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
         po::value<string>(&config.outputFilename)->default_value(
             config.outputFilename),
         ": output directory")
        ("config,c", 
         po::value<string>(&config.configFilename),
         ": configuration file (optionName=value) (ignored)")
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
    if (!config.filenames.empty())
        config.peptide_id =
            shared_ptr<PeptideID>(
                new PeptideID_pepXml(config.filenames[0])
                );

    config.usageOptions = usageOptions;

    return config;
}

// Strips out the string before the first "." and after "." in the
// string. Probably not needed. 
string actualPeptide(const string& peptide)
{
    string p;
    size_t first = peptide.find_first_of(".");
    size_t last = peptide.find_last_of(".");

    if (first != string::npos && first != last)
    {
        p = peptide.substr(first, last);
    }
    
    return p;
}


void tallyRecord(const PeptideID::Record& record, Tally& tally)
{
    string peptide =  actualPeptide(record.sequence);

    tally.peptides.push_back(peptide);
    tally.proteins.push_back(record.protein_descr);

    typedef vector<string>::iterator iterator;

    iterator i=find(tally.uniquePeptides.begin(),
                    tally.uniquePeptides.end(),
                    peptide);

    if(i == tally.uniquePeptides.end())
        tally.uniquePeptides.push_back(peptide);

    i=find(tally.uniqueProteins.begin(),
           tally.uniqueProteins.end(),
           peptide);

    if(i == tally.uniqueProteins.end())
        tally.uniqueProteins.push_back(record.protein_descr);
}


int main(int argc, const char* argv[])
{
    size_t tick1 = clock();

    namespace bfs = boost::filesystem;
    try
    {
        Config config = parseCommandArgs(argc, argv);

        if (config.filenames.empty())
            throw runtime_error(usage(config).c_str());

        vector<PeptideID::Record> records;

        PeptideID::Iterator j = config.peptide_id->begin();
        for (; j!=config.peptide_id->end(); j++)
        {
            PeptideID::Record record(*j);
            records.push_back(record);
        }

        nativeID_less nil;
        sort(records.begin(), records.end(), nil);

        Tally tally;

        cout << "native ID" << "\t"
             << "# peptides" << "\t"
             << "# proteins" << "\t"
             << "unique peptides" << "\t"
             << "unique proteins" << "\t"
             << "seq. count" << "\t"
             << "sequence" << "\t"
             << "# proteins" << "\t"
             << "proteins" << "\n";
        
        for (vector<PeptideID::Record>::const_iterator i=records.begin();
             i!=records.end(); i++)
        {
            tallyRecord((*i), tally);

            cout << (*i).nativeID << "\t";
            cout << tally.peptides.size() << "\t"
                 << tally.proteins.size() << "\t"
                 << tally.uniquePeptides.size() << "\t"
                 << tally.uniqueProteins.size() << "\t"
                 << ((*i).sequence.size() > 0 ?
                     (*i).sequence : "-") << "\t"
                 << ((*i).protein_descr.size() > 0 ?
                     (*i).protein_descr : "-") << "\n";
        }

        size_t tick2 = clock();

//        cout << "delta t = " << (tick2 - tick1) << endl;
    }
    catch(exception& e)
    {
        cerr << e.what() << endl;
    }
    catch(...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}

