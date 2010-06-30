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
#include "pwiz/data/mziddata/DelimReader.hpp"
#include "pwiz/data/mziddata/MzIdentMLFile.hpp"
#include "pwiz/data/mziddata/Serializer_mzid.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/Version.hpp"

#include <iostream>
#include <stdexcept>
#include <fstream>
#include <boost/algorithm/string.hpp>
#include <boost/program_options.hpp>
#include <boost/filesystem.hpp>

using namespace std;
using namespace pwiz::mziddata;
using namespace pwiz::util;

struct Config
{
    Config()
        : expmz(true), headers(false), verbose(false), useStdout(false)
    {}
    
    string usageOptions;
    
    string firstfile;
    string secondfile;
    string outputdir;
    
    char delim;

    bool expmz;
    bool headers;
    bool verbose;
    bool useStdout;
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
        ("calcmz,c",
         ": Use calculated m/z")
        ("delim,d",
         po::value<char>(&config.delim)->default_value('\t'),
         ": delimiter separating fields")
        ("expmz,e",
         ": Use experimental m/z (default)")
        ("headers",
         ": Use/read a header in the file")
        ("stdout,s",
         ": Output to standard out instead of file.")
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

    if (vm.count("calcmz"))
        config.expmz = false;
    
    if (vm.count("expmz"))
        config.expmz = true;
    
    if (vm.count("headers"))
        config.headers = true;
    
    config.usageOptions = usageOptions;
    
    return config;
}

void translateToTxt(Config& config)
{
    // Read in mzid file.
    MzIdentMLFile mzid(config.firstfile);
    
    // Open up output file.
    ostream* os;
    if (config.useStdout)
        os = &cout;
    else
        os = new ofstream(config.secondfile.c_str());
    
    
    // Create writer and dump the mzid file to os.
    DelimWriter writer(os, config.delim, config.headers);
    writer(mzid);

    // If we created an ofstream, then close it now.
    if (!config.useStdout && os)
        delete os;
}

void translateToMzid(Config& config, DelimReader dr)
{
    MzIdentML mzid;
    
    dr.read(config.firstfile, read_file_header(config.firstfile), mzid);

    ostream* os = 0;
    if (config.useStdout)
        os = &cout;
    else
        os = new ofstream(config.secondfile.c_str());

    Serializer_mzIdentML serializer;
    serializer.write(*os, mzid);

}

int main(int argc, const char* argv[])
{
    try
    {
        Config config = parseCommandLine(argc, argv);

        // If there's no output file and no stdout flag, throw an
        // error.
        if (config.secondfile.size()==0 && config.useStdout==false)
            throw invalid_argument(usage(config).c_str());

        DelimReader dr;
        if (boost::iends_with(config.firstfile, ".mzid"))
        {
            translateToTxt(config);
        }
        else if (dr.accept(config.firstfile,
                           read_file_header(config.firstfile)))
        {
            translateToMzid(config, dr);
        }

        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "[mzidtxt] Caught unknown exception.\n";
    }

    return 1;
}
