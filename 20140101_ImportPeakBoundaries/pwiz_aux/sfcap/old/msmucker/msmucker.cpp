//
// $Id$
//
// Robert Burke <robert.burke@cshs.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
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



#include <iostream>
#include <fstream>
#include <string>
#include <vector>
#include <boost/program_options.hpp>
#include <boost/filesystem.hpp>

#include "msrun/MSRun.hpp"
#include "msrun/MSRunWriter.hpp"
#include "msmucker/MuckedMSRun.hpp"

using namespace std;
using namespace pwiz::msrun;
using namespace pwiz::msmucker;

namespace po = boost::program_options;

class MySieve : public MSRunWriter::Sieve
{
public:
    virtual bool allow(const Scan& scan) const
    {
        return true;
    }
};

struct Config
{
    string in_filename;
    string out_filename;
    long scanNumber;
    double muckAmount;

    Config()
        : scanNumber(1), muckAmount(0.)
    {}

    Config(const string& in_filename, const string& out_filename,
           long scanNumber, double muckAmount)
        : in_filename(in_filename), out_filename(out_filename),
          scanNumber(scanNumber), muckAmount(muckAmount)
    {}
};

Config processCommandLine(int argc, char** argv)
{

    Config config;

    ostringstream usage;
    usage << "Usage: msmucker [options] in_filename out_filename\n";

    // Construct the commandline parse options
    po::options_description od_config("Options");
    od_config.add_options()
        ("amount,a",
         po::value<double>(&config.muckAmount)->default_value(
             config.muckAmount),
         ": Set the amount to muck up the scan by")
        ("scan,s",
         po::value<long>(&config.scanNumber)->default_value(
             config.scanNumber),
         ": Set the scan to muck up")
         ;

    usage << od_config;

    const char* label_in_filename = "in_filename";
    const char* label_out_filename = "out_filename";

    po::options_description od_args;
    od_args.add_options()
        (label_in_filename, po::value<string>(&config.in_filename), "")
        (label_out_filename, po::value<string>(&config.out_filename), "");

    po::positional_options_description pod_args;
    pod_args.add(label_in_filename, 1);
    pod_args.add(label_out_filename, 1);

    po::options_description op_parse;
    op_parse.add(od_config).add(od_args);

    // Parse the command line

    po::variables_map vm;
    po::store(po::command_line_parser(argc, argv).
              options(op_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    if (config.in_filename.size() == 0 || config.out_filename.size() == 0)
        throw runtime_error(usage.str());
    
    return config;
}

int main(int argc, char** argv)
{
    Config config = processCommandLine(argc, argv);

    auto_ptr<MSRun> msrun = MSRun::create(config.in_filename);

    MuckedMSRun muckedmsrun(msrun, config.scanNumber, config.muckAmount);

    MySieve ms;
    auto_ptr<MSRunWriter> writer = MSRunWriter::create((MSRun*)&muckedmsrun,
                                                       config.out_filename,
                                                       &ms);

    writer->write();
}

