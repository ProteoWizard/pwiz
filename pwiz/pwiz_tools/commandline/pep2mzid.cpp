//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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
#include "pwiz/data/identdata/Pep2MzIdent.hpp"
#include "pwiz/data/identdata/KwCVMap.hpp"
#include "pwiz/data/common/CVTranslator.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "pwiz/data/common/cv.hpp"
#include "pwiz/Version.hpp"
#include "pwiz/data/proteome/Peptide.hpp"
#include "pwiz/utility/misc/Std.hpp"

#include <boost/program_options.hpp>
#include <boost/filesystem.hpp>
#include <boost/lexical_cast.hpp>
#include <boost/tokenizer.hpp>

using boost::tokenizer;

using namespace boost::filesystem;

using namespace pwiz::data;
using namespace pwiz::identdata;
using namespace pwiz::data::pepxml;

struct Config
{
    Config() : verbose(false) {}
    
    string usageOptions;

    vector<string> files;
    string inputFilename;
    string outputFilename;
    string outDirectory;
    vector<string> cvMapFiles;
    bool   debug;
    bool   verbose;
};


string usage(const Config& config)
{
    ostringstream oss;
    
    oss << "Usage: mspicture [options] [input_filename]\n"
        << "Mass Spec Picture - command line access to mass spec data files with optional peptide annotation\n"
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
//        << "ProteoWizard release: " << pwiz::Version::str() << endl
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
         po::value<string>(&config.outDirectory)->default_value(
             config.outDirectory),
         ": output directory")
        ("inputfile,i",
         po::value<string>(&config.inputFilename)->default_value(
             config.inputFilename),
         ": input file name")
        ("outputfile,o",
         po::value<string>(&config.outputFilename)->default_value(
             config.outputFilename),
         ": output file name")
        ("map,m",
         po::value< vector<string> >(&config.cvMapFiles),
        ": keyword to cv map file")
        ("debug,d", ": prints debug information.")
        ("verbose,v", ": prints extra information.")
        ("help,h",
            ": print this helpful message.")
        ;

    ostringstream temp;
    temp << od_config;
    usageOptions = temp.str();

    // handle positional arguments

    po::options_description od_args1;

    const char* label_file = "files";
    od_args1.add_options()(label_file,
                           po::value< vector<string> >(&config.files),
                           "");

    //po::options_description od_args2;
    //const char* label_outputfile = "outputfile";
    //od_args2.add_options()(label_outputfile,
    //                       po::value<string>(&config.outputFilename),
    //                       "");

    po::positional_options_description pod_args;
    pod_args.add(label_file, -1);
    //pod_args.add(label_outputfile, 1);

    
    po::options_description od_parse;
    od_parse.add(od_config).add(od_args1);
    //od_parse.add(od_config).add(od_args2);

    // parse command line

    po::variables_map vm;
    char** argv_hack = (char**)argv;
    po::store(po::command_line_parser(argc, argv_hack).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    config.usageOptions = usageOptions;
    
    if (vm.count("help"))
        throw runtime_error(usage(config).c_str());

    if (vm.count("verbose"))
        config.verbose = true;
    
    if (vm.count("debug"))
        config.debug = true;

    if (config.files.size())
        config.inputFilename = config.files.at(0);

    if (config.files.size()>1)
        config.outputFilename = config.files.at(1);

    return config;
}

int main(int argc, const char* argv[])
{
    namespace pepxml = pwiz::data::pepxml;

    try
    {
        Config config = parseCommandArgs(argc, argv);
        if (config.inputFilename.empty())
            throw runtime_error(config.usageOptions.c_str());

        // TODO read in kw->cv map file.
        vector<CVMapPtr> cvmaps;
        for (vector<string>::iterator i=config.cvMapFiles.begin();
             i != config.cvMapFiles.end(); i++)
        {
            ifstream mapis((*i).c_str());
            mapis >> cvmaps;
        }

        ifstream in(config.inputFilename.c_str());

        MSMSPipelineAnalysis msmsPA;
        msmsPA.read(in);

        Pep2MzIdent p2m(msmsPA);

        p2m.setVerbose(config.verbose);
        p2m.setDebug(config.debug);
        p2m.setParamMap(cvmaps);
        
        p2m.translate();

        IdentDataFile::write(*p2m.getIdentData(), config.outputFilename);
        
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}
