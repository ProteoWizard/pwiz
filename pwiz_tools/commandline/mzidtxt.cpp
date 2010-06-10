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

#include "pwiz/data/mziddata/DelimWriter.hpp"
#include "pwiz/data/mziddata/MzIdentMLFile.hpp"
#include "pwiz/Version.hpp"

#include <iostream>
#include <fstream>
#include <boost/algorithm/string.hpp>
#include <boost/program_options.hpp>
#include <boost/filesystem.hpp>

using namespace std;
using namespace pwiz::mziddata;

struct Config
{
    string usageOptions;
    
    string firstfile;
    string secondfile;
    string outputdir;
    
    char delim;

    bool headers;
    bool verbose;
    bool toMzid;
};

string usage(const Config& config)
{
    ostringstream oss;
    
    oss << "Usage: mzidtxt [options] [input_filename] [output_filename]\n"
        << "mzIdentML Text - command line translator from mzIdentML to text\n"
        << "\n"
        << "Options:\n" 
        << "\n"
        << config.usageOptions
        << "\n";
    
    oss << endl
        << "Questions, comments, and bug reports:\n"
        << "http://proteowizard.sourceforge.net\n"
        << "support@proteowizard.org\n"
        << "\n"
        << "ProteoWizard release: " << pwiz::Version::str() << endl
        << "Build date: " << __DATE__ << " " << __TIME__ << endl;

    return oss.str();
}

Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;

    Config config;
    
    string usageOptions;

    po::options_description od_config("");
    od_config.add_options()
        /*
        ("mzid,m",
         po::value<string>(&config.firstfile)->default_value(
             config.firstfile),
         ": mzid file name")
        ("txt,t",
         po::value<string>(&config.secondfile)->default_value(
             config.secondfile),
         ": delimited file name")
        */
        ("delim,d",
         po::value<char>(&config.delim)->default_value('\t'),
         ": delimiter separating fields")
        ("headers,h",
         ": Use/read a header in the file")
        ("verbose,v", ": prints extra information.")
        ("help,h",
            ": print this helpful message.")
        ;

    ostringstream temp;
    temp << od_config;
    usageOptions = temp.str();

    // handle positional arguments

    po::options_description od_args1;

    const char* label_inputfile = "inputfile";
    od_args1.add_options()(label_inputfile,
                           po::value<string>(&config.firstfile),
                           "");

    po::options_description od_args2;
    const char* label_outputfile = "outputfile";
    od_args2.add_options()(label_outputfile,
                           po::value<string>(&config.secondfile),
                           "");

    po::positional_options_description pod_args;
    pod_args.add(label_inputfile, 1);
    pod_args.add(label_outputfile, 1);

    
    po::options_description od_parse;
    od_parse.add(od_config).add(od_args1);
    od_parse.add(od_config).add(od_args2);

    // parse command line

    po::variables_map vm;
    char** argv_hack = (char**)argv;
    po::store(po::command_line_parser(argc, argv_hack).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    if (vm.count("headers"))
        config.headers = true;
    else
        config.headers = false;
    
    config.usageOptions = usageOptions;
    
    return config;
}

void translateToTxt(Config& config)
{
    // Read in mzid file.
    MzIdentMLFile mzid(config.firstfile);
    
    // Open up output file.
    ofstream os;
    os.open(config.secondfile.c_str());
    
    // Create writer and dump the mzid file to os.
    DelimWriter writer(&os, config.delim, config.headers);
    writer(mzid);
}

int main(int argc, const char* argv[])
{
    try
    {
        Config config = parseCommandLine(argc, argv);

        if (!config.firstfile.size())
        {
            cerr << "no mzid file!\n";
            throw runtime_error(usage(config));
        }

        if (!config.secondfile.size())
        {
            cerr << "no text file!\n";
            throw runtime_error(usage(config));
        }

        if (boost::iends_with(config.firstfile, ".mzid"))
            translateToTxt(config);
        else
            throw runtime_error("text import into mzid not supported yet.");
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "[msdiff] Caught unknown exception.\n";
    }

    return 1;
}
