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

#include "pwiz/data/identdata/DelimWriter.hpp"
#include "pwiz/data/identdata/DelimReader.hpp"
#include "pwiz/data/identdata/IdentDataFile.hpp"
#include "pwiz/data/identdata/Serializer_mzid.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/Version.hpp"

#include <vector>
#include <iostream>
#include <stdexcept>
#include <fstream>
#include <boost/algorithm/string.hpp>
#include <boost/program_options.hpp>
#include <boost/filesystem.hpp>

using namespace std;
using namespace pwiz::identdata;
using namespace pwiz::util;

struct Config
{
    Config()
        : expmz(true), force(false),
          headers(false), verbose(false), useStdout(false)
    {}
    
    string usageOptions;

    // TODO useful?
    vector<string> files;
    
    // TODO keep or throw?
    string firstfile;
    string secondfile;
    string outputdir;
    
    char delim;

    bool expmz;
    bool force;
    bool headers;
    bool verbose;
    bool useStdout;
    bool useStdin;
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
         ": delimiter separating fields. Default is tab")
        ("expmz,e",
         ": Use experimental m/z (default)")
        ("force,f",
         ": Overwrites any existing files." )
        ("headers",
         ": Use/read a header in the file")
        ("input",
         po::value<string>(&config.firstfile),
         ": input file")
        ("output,o",
         po::value<string>(&config.secondfile),
         ": output file")
        ("stdout,s",
         ": Output to standard out instead of file.")
        ("stdin,i",
         ": Input from standard in instead of file.")
        ("verbose,v", ": prints extra information.")
        ("help,h",
            ": print this helpful message.")
        ;

    ostringstream temp;
    temp << od_config;
    usageOptions = temp.str();

    // handle positional arguments

    /*
    const char* label_inputfile = "input";
    po::options_description od_args1;
    od_args1.add_options()(label_inputfile,
                           po::value<string>(&config.firstfile),
                           "");

    po::positional_options_description pod_args;
    pod_args.add(label_inputfile, 1);
    
    po::options_description od_parse;
    od_parse.add(od_config).add(od_args1);
    */
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

    if (vm.count("calcmz"))
        config.expmz = false;
    
    if (vm.count("expmz"))
        config.expmz = true;
    else
        config.expmz = false;
    
    if (vm.count("force"))
        config.force = true;
    
    if (vm.count("headers"))
        config.headers = true;

    if (vm.count("stdout"))
        config.useStdout = true;

    if (vm.count("stdin"))
        config.useStdin = true;

    if (config.files.size()>0)
        config.firstfile = config.files.at(0);

    if (config.files.size()>1)
        config.secondfile = config.files.at(1);
    
    config.usageOptions = usageOptions;
    
    return config;
}

void translateToTxt(Config& config)
{
    namespace bfs=boost::filesystem;

    // Read in mzid file.
    IdentDataFile mzid(config.firstfile);
    
    // Open up output file.
    string secondfile;
    ostream* os = NULL;
    if (config.useStdout)
        os = &cout;
    else if (config.secondfile.empty())
    {
        // Select the most appropriate extension by delimiter.
        string ext("txt");
        switch(config.delim)
        {
        case '\t':
            ext = "tab";
            break;

        case ',':
            ext = "csv";
            break;

        default:
            break;
        }
        
        // Create a txt file w/ appropriate extension.
        bfs::path secondpath(config.firstfile);
        secondpath.replace_extension(ext);

        secondfile = secondpath.string();
    }
    else
        secondfile = config.secondfile;
    
    // Check if the second file exists and we're not forcing.
    if (!config.useStdout && !secondfile.empty() &&
        bfs::exists(secondfile) && !config.force)
        throw runtime_error(("File "+secondfile+" exists.\n"
                             " Use -f to override.").c_str());
    
    if (!os)
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
    namespace bfs=boost::filesystem;

    IdentData mzid;
    
    dr.read(config.firstfile, read_file_header(config.firstfile), mzid);

    ostream* os = 0;
    string secondfile;
    if (config.useStdout)
        os = &cout;
    else if (config.secondfile.empty())
    {
        bfs::path secondpath(config.firstfile);
        secondpath.replace_extension("mzid");

        secondfile = secondpath.string();
    }
    else
        secondfile = config.secondfile;

    if (!config.useStdout && !secondfile.empty() &&
        bfs::exists(secondfile) && !config.force)
        throw runtime_error(("File "+secondfile+" exists.\n"
                             " Use -f to override.").c_str());
    
    if (!os)
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
        if (config.firstfile.size()==0)
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
