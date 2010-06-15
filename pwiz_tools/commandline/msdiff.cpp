//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/Diff.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include "pwiz/Version.hpp"
#include "boost/program_options.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::msdata;


struct Config
{
    vector<string> filenames;
    DiffConfig diffConfig;
};


ostream& operator<<(ostream& os, const Config& config)
{
    os << "filenames: ";
    copy(config.filenames.begin(), config.filenames.end(), ostream_iterator<string>(os," "));
    os << endl
       << "precision: " << config.diffConfig.precision << endl
       << "ignore meta-data: " << boolalpha << config.diffConfig.ignoreMetadata << endl;
    return os;
}


Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;

    ostringstream usage;
    usage << "Usage: msdiff [options] filename1 filename2\n"
          << "Compare two mass spec data files.\n\n";

    Config config;

    po::options_description od_config("Options");
    od_config.add_options()
        ("precision,p",
            po::value<double>(&config.diffConfig.precision)
                ->default_value(config.diffConfig.precision),
            ": set floating point precision for comparing binary data")
        ("ignore,i",
            po::value<bool>(&config.diffConfig.ignoreMetadata)
                ->default_value(config.diffConfig.ignoreMetadata)
                ->zero_tokens(),
            ": ignore metadata (compare scan binary data and important scan metadata only)")
   	     ;

    // append options description to usage string

    usage << od_config;

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
    po::store(po::command_line_parser(argc, (char**)argv).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    // remember filenames from command line

    if (vm.count(label_args))
        config.filenames = vm[label_args].as< vector<string> >();

    // check stuff

    usage << endl
          << "Questions, comments, and bug reports:\n"
          << "http://proteowizard.sourceforge.net\n"
          << "support@proteowizard.org\n"
          << "\n"
          << "ProteoWizard release: " << pwiz::Version::str() << endl
          << "Build date: " << __DATE__ << " " << __TIME__ << endl;

    if (config.filenames.size() != 2)
        throw runtime_error(usage.str());

    return config;
}


int do_diff(const Config& config)
{
    ExtendedReaderList readers;

    MSDataFile msd1(config.filenames[0], &readers);
    MSDataFile msd2(config.filenames[1], &readers);

    Diff<MSData, DiffConfig> diff(msd1, msd2, config.diffConfig);
    if (diff) cout << diff; 
    return diff;
}


int main(int argc, const char* argv[])
{
    try
    {
        Config config = parseCommandLine(argc, argv);        
        return do_diff(config);
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

